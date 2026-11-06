#!/usr/bin/env python3
"""Offline and deployment-runner checks for the StripeTest target contract."""

from __future__ import annotations

import json
import os
import re
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
TARGET_FILE = ROOT / "infra/stripetest/target.json"
MAIN_PARAMETERS_FILE = ROOT / "infra/stripetest/main.parameters.json"
APPLICATION_PARAMETERS_FILE = ROOT / "infra/stripetest/application.parameters.json"
APPLICATION_TEMPLATE_FILE = ROOT / "infra/stripetest/application.bicep"
ACR_PULL_ROLE_DEFINITION_ID = "7f951dda-4ed3-4680-a7ca-43fe172d538d"


def _read_json(path: Path) -> dict[str, Any]:
    with path.open(encoding="utf-8") as file:
        return json.load(file)


def _flatten_strings(value: Any) -> list[str]:
    if isinstance(value, str):
        return [value]
    if isinstance(value, dict):
        return [item for nested in value.values() for item in _flatten_strings(nested)]
    if isinstance(value, list):
        return [item for nested in value for item in _flatten_strings(nested)]
    return []


def validate_target(environment: dict[str, str] | None = None) -> None:
    environment = os.environ if environment is None else environment
    target = _read_json(TARGET_FILE)
    target_strings = _flatten_strings(target)
    if any("replace_with" in value.casefold() for value in target_strings):
        raise ValueError("StripeTest target manifest still contains unresolved placeholders.")
    if target["location"].casefold() != "eastus":
        raise ValueError("StripeTest region must remain the reviewed eastus target.")
    if target["database"]["name"] != "stayflow_stripe_test":
        raise ValueError("StripeTest database name must remain the isolated test database.")
    if target["containerRegistry"]["repositories"] != {
        "backend": "stayflow-api",
        "frontend": "stayflow-web",
    }:
        raise ValueError("StripeTest images must use the dedicated application repositories.")

    validate_target_environment(target, environment)

    image_sha = environment.get("GITHUB_SHA", "")
    if not re.fullmatch(r"[0-9a-f]{40}", image_sha):
        raise ValueError("The workflow commit SHA is not a full immutable Git SHA.")

    main_parameters = _read_json(MAIN_PARAMETERS_FILE)["parameters"]
    application_parameters = _read_json(APPLICATION_PARAMETERS_FILE)["parameters"]
    mappings = {
        "main.parameters.json": (
            main_parameters,
            {
                "location": target["location"],
                "containerAppsEnvironmentName": target["resources"]["containerAppsEnvironment"],
                "postgresServerName": target["database"]["serverName"],
                "postgresDatabaseName": target["database"]["name"],
                "keyVaultName": target["resources"]["keyVault"],
                "logAnalyticsName": target["resources"]["logAnalytics"],
                "applicationInsightsName": target["resources"]["applicationInsights"],
                "virtualNetworkName": target["resources"]["virtualNetwork"],
                "virtualNetworkAddressPrefix": target["network"]["virtualNetworkAddressPrefix"],
                "containerAppsSubnetAddressPrefix": target["network"]["containerAppsSubnetAddressPrefix"],
                "postgresSubnetAddressPrefix": target["network"]["postgresSubnetAddressPrefix"],
                "privateEndpointSubnetAddressPrefix": target["network"]["privateEndpointSubnetAddressPrefix"],
                "runtimeIdentityName": target["resources"]["runtimeIdentity"],
                "imagePullIdentityName": target["resources"]["imagePullIdentity"],
                "containerRegistryName": target["containerRegistry"]["name"],
                "containerRegistryResourceGroupName": target["containerRegistry"]["resourceGroupName"],
            },
        ),
        "application.parameters.json": (
            application_parameters,
            {
                "containerRegistryName": target["containerRegistry"]["name"],
                "containerRegistryResourceGroupName": target["containerRegistry"]["resourceGroupName"],
                "containerAppsEnvironmentName": target["resources"]["containerAppsEnvironment"],
                "keyVaultName": target["resources"]["keyVault"],
                "applicationInsightsName": target["resources"]["applicationInsights"],
                "runtimeIdentityName": target["resources"]["runtimeIdentity"],
                "imagePullIdentityName": target["resources"]["imagePullIdentity"],
                "postgresServerName": target["database"]["serverName"],
                "postgresDatabaseName": target["database"]["name"],
                "backendContainerAppName": target["resources"]["backendApp"],
                "frontendContainerAppName": target["resources"]["frontendApp"],
            },
        ),
    }
    for filename, (parameters, expected_values) in mappings.items():
        for key, expected in expected_values.items():
            if parameters.get(key, {}).get("value") != expected:
                raise ValueError(f"{filename}:{key} does not match the reviewed target manifest.")

    required_application_parameters = {
        "imageSha",
        "backendImage",
        "frontendImage",
        *mappings["application.parameters.json"][1],
    }
    if required_application_parameters.difference(application_parameters):
        raise ValueError("application.parameters.json is missing required target parameters.")
    if any(
        application_parameters[name].get("value") != ""
        for name in ("backendImage", "frontendImage")
    ):
        raise ValueError("Application image parameter defaults must be empty; digests are required.")

    validate_safety_contract()


def validate_target_environment(
    target: dict[str, Any],
    environment: dict[str, str],
) -> None:
    required = {
        "STRIPETEST_EXPECTED_SUBSCRIPTION_ID": target["subscriptionId"],
        "STRIPETEST_EXPECTED_TENANT_ID": target["tenantId"],
        "STRIPETEST_RESOURCE_GROUP": target["resourceGroupName"],
        "STRIPETEST_REGION": target["location"],
        "STRIPETEST_ACR_NAME": target["containerRegistry"]["name"],
        "STRIPETEST_ACR_RESOURCE_GROUP": target["containerRegistry"]["resourceGroupName"],
        "STRIPETEST_ACR_LOGIN_SERVER": target["containerRegistry"]["loginServer"],
        "STRIPETEST_ACR_RESOURCE_ID": target["containerRegistry"]["resourceId"],
        "STRIPETEST_POSTGRES_SERVER_NAME": target["database"]["serverName"],
        "STRIPETEST_DATABASE_HOST": target["database"]["host"],
        "STRIPETEST_DATABASE_NAME": target["database"]["name"],
        "AZURE_SUBSCRIPTION_ID": target["subscriptionId"],
        "AZURE_TENANT_ID": target["tenantId"],
        "AZURE_RESOURCE_GROUP": target["resourceGroupName"],
    }
    for key, expected in required.items():
        actual = environment.get(key, "")
        if not actual or actual != expected:
            raise ValueError(f"{key} is missing or does not match the reviewed StripeTest target.")

    expected_host = f'{target["database"]["serverName"]}.postgres.database.azure.com'
    if target["database"]["host"].casefold() != expected_host.casefold():
        raise ValueError("The approved database host does not match the dedicated PostgreSQL server name.")
    registry_login_server = target["containerRegistry"]["loginServer"]
    if (
        not registry_login_server
        or "/" in registry_login_server
        or ":" in registry_login_server
        or registry_login_server != registry_login_server.casefold()
    ):
        raise ValueError("The approved ACR login server is malformed.")


def validate_safety_contract(application_template: str | None = None) -> None:
    if application_template is None:
        application_template = APPLICATION_TEMPLATE_FILE.read_text(encoding="utf-8")
    required_values = {
        "StayFlow__EnvironmentIdentity": "'StripeTest'",
        "Billing__Provider": "'Stripe'",
        "Billing__StripeTestModeExpected": "'true'",
        "Billing__StripeApiVersion": "'2024-06-20'",
        "Billing__UsageBilling__Enabled": "'false'",
        "Billing__UsageBilling__Worker__Enabled": "'false'",
        "Billing__UsageBilling__OpenPeriodInvoiceSafetyVerified": "'false'",
    }
    for key, value in required_values.items():
        pattern = (
            rf"(?m)^[ \t]*name:\s*'{re.escape(key)}'[ \t]*\r?\n"
            rf"[ \t]+value:\s*{re.escape(value)}[ \t]*$"
        )
        if not re.search(pattern, application_template):
            raise ValueError(f"{key} has an unexpected value in the application template.")

    required_mappings = {
        "StayFlow__StripeTest__Database__ApprovedHost": r"value:\s*postgresHost",
        "StayFlow__StripeTest__Database__ApprovedDatabase": r"value:\s*postgresDatabaseName",
        "ConnectionStrings__DefaultConnection": r"secretRef:\s*'database-connection'",
        "Jwt__SigningKey": r"secretRef:\s*'jwt-signing-key'",
        "Billing__StripeSecretKey": r"secretRef:\s*'stripe-secret-key'",
        "Billing__StripeWebhookSigningSecret": r"secretRef:\s*'stripe-webhook-secret'",
        "Billing__PlanPriceIds__Starter": r"secretRef:\s*'starter-price-id'",
        "Billing__PlanPriceIds__Professional": r"secretRef:\s*'professional-price-id'",
        "Billing__CheckoutSuccessUrl": r"value:\s*'\$\{frontendOrigin\}/host/settings/billing\?checkout=success'",
        "Billing__CheckoutCancelUrl": r"value:\s*'\$\{frontendOrigin\}/host/settings/billing\?checkout=cancel'",
        "Billing__BillingPortalReturnUrl": r"value:\s*'\$\{frontendOrigin\}/host/settings/billing'",
        "Cors__AllowedOrigins__0": r"value:\s*frontendOrigin",
        "ProductionHardening__Security__AllowedOrigins__0": r"value:\s*frontendOrigin",
    }
    for key, value_pattern in required_mappings.items():
        if not re.search(
            rf"(?m)^[ \t]*name:\s*'{re.escape(key)}'[ \t]*\r?\n"
            rf"[ \t]+{value_pattern}[ \t]*$",
            application_template,
        ):
            raise ValueError(f"{key} has an unexpected mapping in the application template.")


def validate_image_reference(
    image_reference: str,
    image_kind: str,
    target: dict[str, Any] | None = None,
) -> None:
    target = _read_json(TARGET_FILE) if target is None else target
    if any("replace_with" in value.casefold() for value in _flatten_strings(target)):
        raise ValueError("StripeTest target manifest still contains unresolved placeholders.")
    if image_kind not in {"backend", "frontend"}:
        raise ValueError("Image kind must be backend or frontend.")

    registry = target["containerRegistry"]["loginServer"].rstrip("/")
    repository = target["containerRegistry"]["repositories"][image_kind]
    prefix, separator, digest = image_reference.partition("@")
    reference_parts = prefix.split("/", 1)
    if (
        not separator
        or len(reference_parts) != 2
        or reference_parts[0].casefold() != registry.casefold()
        or reference_parts[1] != repository
        or not re.fullmatch(r"sha256:[0-9a-f]{64}", digest)
    ):
        raise ValueError(
            f"{image_kind} image must use the exact approved registry/repository and a SHA-256 digest."
        )


def image_reference_from_metadata(
    metadata_path: Path,
    image_kind: str,
    target: dict[str, Any] | None = None,
) -> str:
    try:
        metadata = _read_json(metadata_path)
    except (OSError, json.JSONDecodeError) as error:
        raise ValueError("Buildx image metadata is missing or invalid.") from error
    digest = metadata.get("containerimage.digest")
    if not isinstance(digest, str) or not re.fullmatch(r"sha256:[0-9a-f]{64}", digest):
        raise ValueError("Buildx did not return a valid pushed-image digest.")
    target = _read_json(TARGET_FILE) if target is None else target
    registry = target["containerRegistry"]["loginServer"].rstrip("/")
    repository = target["containerRegistry"]["repositories"].get(image_kind)
    if not isinstance(repository, str):
        raise ValueError("Image kind must be backend or frontend.")
    reference = f"{registry}/{repository}@{digest}"
    validate_image_reference(reference, image_kind, target)
    return reference


def validate_acr_pull_role(
    assignments: Any,
    principal_id: str,
    registry_resource_id: str,
    target: dict[str, Any] | None = None,
) -> None:
    target = _read_json(TARGET_FILE) if target is None else target
    expected_registry_id = target["containerRegistry"]["resourceId"]
    if any("replace_with" in value.casefold() for value in _flatten_strings(target)):
        raise ValueError("StripeTest target manifest still contains unresolved placeholders.")
    if (
        not re.fullmatch(
            r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            principal_id,
        )
        or not registry_resource_id
        or registry_resource_id.casefold() != expected_registry_id.casefold()
        or not isinstance(assignments, list)
    ):
        raise ValueError("The approved ACR or image-pull identity is missing or mismatched.")

    for assignment in assignments:
        if not isinstance(assignment, dict):
            continue
        scope = str(assignment.get("scope", ""))
        assigned_principal = str(assignment.get("principalId", ""))
        role_definition = str(assignment.get("roleDefinitionId", "")).rstrip("/").split("/")[-1]
        if (
            scope.casefold() == expected_registry_id.casefold()
            and assigned_principal.casefold() == principal_id.casefold()
            and role_definition.casefold() == ACR_PULL_ROLE_DEFINITION_ID
        ):
            return
    raise ValueError("The dedicated image-pull identity lacks AcrPull on the exact approved ACR.")


def parse_connection_string(connection_string: str) -> list[tuple[str, str]]:
    entries: list[tuple[str, str]] = []
    position = 0
    while position < len(connection_string):
        while position < len(connection_string) and (
            connection_string[position].isspace() or connection_string[position] == ";"
        ):
            position += 1
        if position == len(connection_string):
            break

        key_start = position
        while position < len(connection_string) and connection_string[position] not in "=;":
            position += 1
        if position == len(connection_string) or connection_string[position] != "=":
            raise ValueError("Malformed PostgreSQL connection string.")

        key = connection_string[key_start:position].strip()
        if not key:
            raise ValueError("Malformed PostgreSQL connection string.")
        position += 1
        while position < len(connection_string) and connection_string[position].isspace():
            position += 1

        if position < len(connection_string) and connection_string[position] in "'\"":
            quote = connection_string[position]
            position += 1
            value: list[str] = []
            closed = False
            while position < len(connection_string):
                character = connection_string[position]
                if character != quote:
                    value.append(character)
                    position += 1
                    continue
                if position + 1 < len(connection_string) and connection_string[position + 1] == quote:
                    value.append(quote)
                    position += 2
                    continue
                position += 1
                closed = True
                break
            if not closed:
                raise ValueError("Malformed PostgreSQL connection string.")
            while position < len(connection_string) and connection_string[position].isspace():
                position += 1
            if position < len(connection_string) and connection_string[position] != ";":
                raise ValueError("Malformed PostgreSQL connection string.")
            entries.append((key, "".join(value).strip()))
        else:
            value_start = position
            while position < len(connection_string) and connection_string[position] != ";":
                position += 1
            entries.append((key, connection_string[value_start:position].strip()))

    return entries


def validate_database_connection(
    connection_string: str,
    approved_host: str,
    approved_database: str,
) -> None:
    entries = parse_connection_string(connection_string)
    normalized = [("".join(key.casefold().split()), value) for key, value in entries]
    keys = [key for key, _ in normalized]
    for key in ("host", "server", "database", "initialcatalog"):
        if keys.count(key) > 1:
            raise ValueError("Ambiguous PostgreSQL target in application connection string.")

    host_values = {
        value.casefold()
        for key, value in normalized
        if key in {"host", "server"}
    }
    database_values = {
        value
        for key, value in normalized
        if key in {"database", "initialcatalog"}
    }
    if (
        len(host_values) != 1
        or len(database_values) != 1
        or next(iter(host_values)) != approved_host.casefold()
        or next(iter(database_values)) != approved_database
        or "," in next(iter(host_values))
    ):
        raise ValueError("Application connection string does not match the approved test database.")

    fallback_keys = {"service", "servicefile", "hostaddr", "targetsessionattributes"}
    if fallback_keys.intersection(keys):
        raise ValueError("Fallback PostgreSQL target settings are not permitted.")

    ssl_modes = [value.casefold() for key, value in normalized if key == "sslmode"]
    if ssl_modes != ["verifyfull"]:
        raise ValueError("Application connection string must require full TLS verification.")


def validate_stripe_test_secret(secret_key: str) -> None:
    if not re.fullmatch(r"(?:sk|rk)_test_[A-Za-z0-9_]+", secret_key):
        raise ValueError("Stripe API credential must be a non-empty test-mode key.")


def main() -> int:
    try:
        if sys.argv[1:] == ["--check-target"]:
            validate_target()
        elif sys.argv[1:] == ["--check-safety"]:
            validate_safety_contract()
        elif sys.argv[1:] == ["--check-connection"]:
            validate_database_connection(
                os.environ.get("STRIPETEST_CONNECTION_STRING", ""),
                os.environ.get("STRIPETEST_DATABASE_HOST", ""),
                os.environ.get("STRIPETEST_DATABASE_NAME", ""),
            )
        elif sys.argv[1:] == ["--check-stripe-secret"]:
            validate_stripe_test_secret(os.environ.get("STRIPETEST_STRIPE_SECRET_KEY", ""))
        elif sys.argv[1:] == ["--check-image-reference"]:
            validate_image_reference(
                os.environ.get("STRIPETEST_IMAGE_REFERENCE", ""),
                os.environ.get("STRIPETEST_IMAGE_KIND", ""),
            )
        elif sys.argv[1:] == ["--image-from-metadata"]:
            reference = image_reference_from_metadata(
                Path(os.environ.get("STRIPETEST_IMAGE_METADATA_FILE", "")),
                os.environ.get("STRIPETEST_IMAGE_KIND", ""),
            )
            print(reference)
            return 0
        elif sys.argv[1:] == ["--check-acr-pull-role"]:
            validate_acr_pull_role(
                json.load(sys.stdin),
                os.environ.get("STRIPETEST_IMAGE_PULL_IDENTITY_PRINCIPAL_ID", ""),
                os.environ.get("STRIPETEST_ACR_RESOURCE_ID", ""),
            )
        else:
            raise ValueError(
                "Use --check-target, --check-safety, --check-connection, "
                "--check-stripe-secret, --check-image-reference, "
                "--image-from-metadata, or --check-acr-pull-role."
            )
    except (OSError, KeyError, json.JSONDecodeError, ValueError) as error:
        print(f"StripeTest preflight failed: {error}", file=sys.stderr)
        return 1
    print("StripeTest preflight passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
