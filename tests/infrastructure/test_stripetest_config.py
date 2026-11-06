import importlib.util
import json
import re
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
POLICY_PATH = ROOT / "infra/stripetest/policy.py"
SPEC = importlib.util.spec_from_file_location("stripetest_policy", POLICY_PATH)
POLICY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(POLICY)


class StripeTestInfrastructureTests(unittest.TestCase):
    def test_infrastructure_and_application_parameters_match_target_manifest(self):
        target = json.loads((ROOT / "infra/stripetest/target.json").read_text())
        main_parameters = json.loads(
            (ROOT / "infra/stripetest/main.parameters.json").read_text()
        )["parameters"]
        application_parameters = json.loads(
            (ROOT / "infra/stripetest/application.parameters.json").read_text()
        )["parameters"]

        self.assertEqual(target["location"], "eastus")
        self.assertEqual(target["database"]["name"], "stayflow_stripe_test")
        self.assertEqual(
            target["containerRegistry"]["repositories"],
            {"backend": "stayflow-api", "frontend": "stayflow-web"},
        )
        self.assertEqual(
            main_parameters["postgresDatabaseName"]["value"],
            target["database"]["name"],
        )
        self.assertEqual(
            application_parameters["postgresDatabaseName"]["value"],
            target["database"]["name"],
        )
        self.assertNotIn("postgresAdminPassword", main_parameters)
        self.assertEqual(application_parameters["backendImage"]["value"], "")
        self.assertEqual(application_parameters["frontendImage"]["value"], "")

        for bicep_name, parameters in (
            ("main.bicep", main_parameters),
            ("application.bicep", application_parameters),
        ):
            source = (ROOT / "infra/stripetest" / bicep_name).read_text()
            required_parameters = {
                name
                for name, has_default in re.findall(
                    r"(?m)^param\s+(\w+)\s+\w+([^\n]*)$",
                    source,
                )
                if "=" not in has_default
            }
            if bicep_name == "main.bicep":
                required_parameters.remove("postgresAdminPassword")
            self.assertTrue(required_parameters.issubset(parameters))

    def test_app_template_uses_isolated_secrets_and_keeps_usage_billing_disabled(self):
        template = (ROOT / "infra/stripetest/application.bicep").read_text()

        POLICY.validate_safety_contract(template)
        self.assertIn("keyVaultUrl:", template)
        self.assertIn("identity: runtimeIdentity.id", template)
        self.assertIn("name: 'StayFlow__EnvironmentIdentity'\n              value: 'StripeTest'", template)
        self.assertIn("name: 'Billing__Provider'\n              value: 'Stripe'", template)
        self.assertIn("name: 'Billing__StripeTestModeExpected'\n              value: 'true'", template)
        self.assertIn("name: 'Billing__StripeApiVersion'\n              value: '2024-06-20'", template)
        for key in (
            "Billing__UsageBilling__Enabled",
            "Billing__UsageBilling__Worker__Enabled",
            "Billing__UsageBilling__OpenPeriodInvoiceSafetyVerified",
        ):
            self.assertRegex(
                template,
                rf"name:\s*'{key}'\s+value:\s*'false'",
            )
        self.assertIn("var postgresHost = '${postgresServer.name}.postgres.database.azure.com'", template)
        self.assertIn(
            "name: 'StayFlow__StripeTest__Database__ApprovedHost'\n              value: postgresHost",
            template,
        )
        self.assertIn(
            "name: 'StayFlow__StripeTest__Database__ApprovedDatabase'\n              value: postgresDatabaseName",
            template,
        )
        self.assertNotIn("stripe_test_mode_unverified", template)

    def test_safety_contract_rejects_missing_or_enabled_usage_billing_flags(self):
        template = (ROOT / "infra/stripetest/application.bicep").read_text()
        for key in (
            "Billing__UsageBilling__Enabled",
            "Billing__UsageBilling__Worker__Enabled",
            "Billing__UsageBilling__OpenPeriodInvoiceSafetyVerified",
        ):
            with self.subTest(key=key):
                unsafe = template.replace(
                    f"name: '{key}'\n              value: 'false'",
                    f"name: '{key}'\n              value: 'true'",
                )
                with self.assertRaisesRegex(ValueError, key):
                    POLICY.validate_safety_contract(unsafe)

    def test_safety_contract_rejects_production_database_or_secret_mappings(self):
        template = (ROOT / "infra/stripetest/application.bicep").read_text()
        unsafe_templates = (
            template.replace("value: postgresHost", "value: 'production.database.azure.com'"),
            template.replace(
                "secretRef: 'stripe-secret-key'",
                "secretRef: 'production-stripe-secret-key'",
            ),
            template.replace(
                "value: '${frontendOrigin}/host/settings/billing?checkout=success'",
                "value: 'https://production.example/success'",
            ),
        )
        for unsafe in unsafe_templates:
            with self.subTest(template="unsafe app configuration"):
                with self.assertRaises(ValueError):
                    POLICY.validate_safety_contract(unsafe)

    def test_stripe_test_guard_remains_fail_closed_and_target_placeholders_block_deployment(self):
        guard = (ROOT / "backend/Configuration/StripeTestEnvironmentGuard.cs").read_text()
        self.assertIn('"stripe_test_mode_unverified"', guard)
        with self.assertRaisesRegex(ValueError, "unresolved placeholders"):
            POLICY.validate_target({})

    def test_database_connection_preflight_requires_exact_target_and_tls(self):
        valid = (
            "Host=pg-stripetest.postgres.database.azure.com;"
            "Port=5432;Database=stayflow_stripe_test;Username=app;"
            "Password=not-a-real-secret;Ssl Mode=VerifyFull"
        )
        POLICY.validate_database_connection(
            valid,
            "pg-stripetest.postgres.database.azure.com",
            "stayflow_stripe_test",
        )

        for invalid in (
            valid.replace("stayflow_stripe_test", "stayflow_production"),
            valid.replace("pg-stripetest.postgres.database.azure.com", "pg-production"),
            valid.replace("Ssl Mode=VerifyFull", "Ssl Mode=Disable"),
            valid + ";Host=pg-production",
            valid + ";HostAddr=192.0.2.1",
            valid.replace("pg-stripetest.postgres.database.azure.com", "pg-stripetest,pg-production"),
        ):
            with self.subTest(invalid="connection rejected"):
                with self.assertRaises(ValueError):
                    POLICY.validate_database_connection(
                        invalid,
                        "pg-stripetest.postgres.database.azure.com",
                        "stayflow_stripe_test",
                    )

    def test_stripe_credential_preflight_accepts_only_test_mode_key_prefixes(self):
        POLICY.validate_stripe_test_secret("sk_test_offline_fixture")
        POLICY.validate_stripe_test_secret("rk_test_offline_fixture")
        for invalid in (
            "",
            "sk_live_offline_fixture",
            "rk_live_offline_fixture",
            "unknown",
            "sk_test_",
            " sk_test_offline_fixture",
            "sk_test_offline fixture",
        ):
            with self.subTest(key_class="rejected"):
                with self.assertRaises(ValueError):
                    POLICY.validate_stripe_test_secret(invalid)

    def test_image_references_require_the_exact_registry_repository_and_digest(self):
        target = {
            "containerRegistry": {
                "loginServer": "stripetest.azurecr.io",
                "repositories": {
                    "backend": "stayflow-api",
                    "frontend": "stayflow-web",
                },
            }
        }
        digest = "a" * 64
        POLICY.validate_image_reference(
            f"stripetest.azurecr.io/stayflow-api@sha256:{digest}",
            "backend",
            target,
        )
        POLICY.validate_image_reference(
            f"stripetest.azurecr.io/stayflow-web@sha256:{digest}",
            "frontend",
            target,
        )

        invalid_references = (
            f"stripetest.azurecr.io/stayflow-api:sha256-{digest}",
            "stripetest.azurecr.io/stayflow-api@sha256:1234",
            f"production.azurecr.io/stayflow-api@sha256:{digest}",
            f"stripetest.azurecr.io/production-api@sha256:{digest}",
            f"stripetest.azurecr.io/stayflow-web@sha256:{digest}",
        )
        for reference in invalid_references:
            with self.subTest(reference=reference):
                with self.assertRaises(ValueError):
                    POLICY.validate_image_reference(reference, "backend", target)

    def test_buildx_digest_metadata_is_required_and_independent_per_image(self):
        target = {
            "containerRegistry": {
                "loginServer": "stripetest.azurecr.io",
                "repositories": {"backend": "stayflow-api", "frontend": "stayflow-web"},
            }
        }
        with tempfile.TemporaryDirectory() as directory:
            metadata = Path(directory) / "buildx.json"
            metadata.write_text(json.dumps({"containerimage.digest": "sha256:" + "b" * 64}))
            self.assertEqual(
                POLICY.image_reference_from_metadata(metadata, "backend", target),
                f"stripetest.azurecr.io/stayflow-api@sha256:{'b' * 64}",
            )
            self.assertEqual(
                POLICY.image_reference_from_metadata(metadata, "frontend", target),
                f"stripetest.azurecr.io/stayflow-web@sha256:{'b' * 64}",
            )
            metadata.write_text(json.dumps({"containerimage.digest": "not-a-digest"}))
            with self.assertRaisesRegex(ValueError, "valid pushed-image digest"):
                POLICY.image_reference_from_metadata(metadata, "backend", target)

    def test_acr_pull_role_must_be_direct_and_match_registry_and_identity(self):
        target = {
            "containerRegistry": {
                "resourceId": "/subscriptions/test/resourceGroups/acr-rg/providers/Microsoft.ContainerRegistry/registries/testacr"
            }
        }
        principal_id = "11111111-2222-3333-4444-555555555555"
        assignment = {
            "scope": target["containerRegistry"]["resourceId"],
            "principalId": principal_id,
            "roleDefinitionId": (
                "/subscriptions/test/providers/Microsoft.Authorization/roleDefinitions/"
                + POLICY.ACR_PULL_ROLE_DEFINITION_ID
            ),
        }
        POLICY.validate_acr_pull_role(
            [assignment],
            principal_id,
            target["containerRegistry"]["resourceId"],
            target,
        )

        for assignments, principal, registry in (
            ([], principal_id, target["containerRegistry"]["resourceId"]),
            ([{**assignment, "scope": "/wrong/scope"}], principal_id, target["containerRegistry"]["resourceId"]),
            ([assignment], "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", target["containerRegistry"]["resourceId"]),
            ([assignment], principal_id, "/wrong/registry"),
            ([{**assignment, "roleDefinitionId": "/roleDefinitions/00000000-0000-0000-0000-000000000000"}], principal_id, target["containerRegistry"]["resourceId"]),
        ):
            with self.subTest(assignment=assignments):
                with self.assertRaises(ValueError):
                    POLICY.validate_acr_pull_role(assignments, principal, registry, target)

    def test_environment_target_rejects_subscription_group_region_registry_and_database_mismatch(self):
        target = {
            "subscriptionId": "sub-id",
            "tenantId": "tenant-id",
            "resourceGroupName": "stripetest-rg",
            "location": "eastus",
            "containerRegistry": {
                "name": "stripetestacr",
                "resourceGroupName": "shared-nonprod-rg",
                "loginServer": "stripetestacr.azurecr.io",
                "resourceId": "/subscriptions/sub-id/resourceGroups/shared-nonprod-rg/providers/Microsoft.ContainerRegistry/registries/stripetestacr",
            },
            "database": {
                "serverName": "stripetest-pg",
                "host": "stripetest-pg.postgres.database.azure.com",
                "name": "stayflow_stripe_test",
            },
        }
        environment = {
            "STRIPETEST_EXPECTED_SUBSCRIPTION_ID": "sub-id",
            "STRIPETEST_EXPECTED_TENANT_ID": "tenant-id",
            "STRIPETEST_RESOURCE_GROUP": "stripetest-rg",
            "STRIPETEST_REGION": "eastus",
            "STRIPETEST_ACR_NAME": "stripetestacr",
            "STRIPETEST_ACR_RESOURCE_GROUP": "shared-nonprod-rg",
            "STRIPETEST_ACR_LOGIN_SERVER": "stripetestacr.azurecr.io",
            "STRIPETEST_ACR_RESOURCE_ID": target["containerRegistry"]["resourceId"],
            "STRIPETEST_POSTGRES_SERVER_NAME": "stripetest-pg",
            "STRIPETEST_DATABASE_HOST": target["database"]["host"],
            "STRIPETEST_DATABASE_NAME": "stayflow_stripe_test",
            "AZURE_SUBSCRIPTION_ID": "sub-id",
            "AZURE_TENANT_ID": "tenant-id",
            "AZURE_RESOURCE_GROUP": "stripetest-rg",
        }
        POLICY.validate_target_environment(target, environment)

        for key, wrong_value in (
            ("AZURE_SUBSCRIPTION_ID", "production-sub"),
            ("AZURE_RESOURCE_GROUP", "production-rg"),
            ("STRIPETEST_REGION", "westus"),
            ("STRIPETEST_ACR_RESOURCE_ID", "/production/acr"),
            ("STRIPETEST_DATABASE_HOST", "production.postgres.database.azure.com"),
            ("STRIPETEST_DATABASE_NAME", "production"),
        ):
            wrong_environment = {**environment, key: wrong_value}
            with self.subTest(key=key):
                with self.assertRaises(ValueError):
                    POLICY.validate_target_environment(target, wrong_environment)

    def test_workflows_are_manual_and_use_separate_environments(self):
        infra = (ROOT / ".github/workflows/provision-stripetest.yml").read_text()
        app = (ROOT / ".github/workflows/deploy-stripetest.yml").read_text()

        for workflow, environment in (
            (infra, "stripetest-infra"),
            (app, "stripetest"),
        ):
            self.assertIn("workflow_dispatch:", workflow)
            self.assertIn(f"environment: {environment}", workflow)
            self.assertNotRegex(workflow, r"(?m)^\s+(push|release):")
            self.assertNotIn("infra/parameters/production.json", workflow)
            self.assertNotIn("infra/parameters/staging.json", workflow)
            self.assertIn("id-token: write", workflow)
            self.assertNotIn("apply-migrations.sh", workflow)
            self.assertNotIn("generate-migration-script.sh", workflow)

            self.assertNotRegex(app, r"(?m)^\s+inputs:")
            self.assertIn("policy.py --check-target", infra)
            self.assertIn("policy.py --check-target", app)
            self.assertIn("policy.py --check-safety", infra)
            self.assertIn("policy.py --check-connection", app)
            self.assertIn("policy.py --check-stripe-secret", app)
            self.assertIn("policy.py --check-image-reference", app)
            self.assertIn("policy.py --check-acr-pull-role", app)
            self.assertIn("policy.py --image-from-metadata", app)
            self.assertIn("runs-on: [self-hosted, linux, x64, stripetest-private]", app)
            self.assertIn("environment: stripetest-images", app)
            self.assertIn("environment: stripetest-private-preflight", app)
            self.assertIn("environment: stripetest", app)
            self.assertIn("needs: validate-private-configuration", app)
            self.assertIn("az keyvault secret show", app)
            self.assertIn("docker buildx build", app)
            self.assertIn("--push", app)
            self.assertIn("needs.build.outputs.backend_image", app)
            self.assertIn("needs.build.outputs.frontend_image", app)
            self.assertIn("backendImage=\"$BACKEND_IMAGE\"", app)
            self.assertIn("frontendImage=\"$FRONTEND_IMAGE\"", app)
            self.assertNotIn("az keyvault", infra.casefold())
            self.assertNotIn("docker push", app)

    def test_public_backend_ingress_is_not_claimed_to_be_path_restricted(self):
        template = (ROOT / "infra/stripetest/application.bicep").read_text()
        documentation = (ROOT / "infra/stripetest/README.md").read_text()

        self.assertIn("external: true", template)
        self.assertIn("targetPort: 8080", template)
        self.assertIn("all backend routes", documentation.casefold())
        self.assertIn("/api/billing/webhook/stripe", documentation)
        self.assertIn("/webhooks/mpesa/stk", documentation)
        self.assertIn("/webhooks/whatsapp", documentation)
        self.assertIn("does not configure outbound", documentation.casefold())

    def test_database_and_vault_are_private_network_resources(self):
        template = (ROOT / "infra/stripetest/main.bicep").read_text()

        self.assertEqual(template.count("publicNetworkAccess: 'Disabled'"), 2)
        self.assertIn("delegatedSubnetResourceId: postgresSubnet.id", template)
        self.assertIn("Microsoft.Network/privateEndpoints@", template)
        self.assertIn("Microsoft.Network/privateDnsZones@", template)

    def test_application_template_has_no_cross_template_role_symbol(self):
        app_template = (ROOT / "infra/stripetest/application.bicep").read_text()
        infra_template = (ROOT / "infra/stripetest/main.bicep").read_text()

        self.assertNotIn("imagePullRoleAssignment", app_template)
        self.assertIn("resource imagePullRoleAssignment", infra_template)
        self.assertIn("scope: containerRegistry", infra_template)
        self.assertIn("principalId: imagePullIdentity.properties.principalId", infra_template)


if __name__ == "__main__":
    unittest.main()
