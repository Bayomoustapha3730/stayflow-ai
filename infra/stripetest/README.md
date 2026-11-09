# StripeTest infrastructure and deployment

This directory defines a separate StripeTest environment. It does not assert that any Azure
resource exists or that the placeholder target has been approved. Replace target placeholders
only after the subscription, resource group, ACR, region, network ranges, resource names, and
external owners are reviewed. Keep real credentials out of these files.

## Deployment boundaries

1. `main.bicep` provisions the dedicated VNet, private PostgreSQL Flexible Server and database,
   private DNS, Key Vault/private endpoint, user-assigned identities, monitoring, and Container
   Apps environment. It does not create database application roles, run SQL, or populate Key Vault.
2. Before app deployment, a separately authorized operator creates a least-privileged PostgreSQL
   application login, stores the TLS-verified connection string and test-only app secrets/Price
   IDs in the dedicated vault, and verifies that the connection string targets the dedicated
   database. Do not copy production data or reuse production credentials.
3. `application.bicep` deploys the separate backend and frontend containers. Its Key Vault
   references require these exact secret names: `stripetest-database-connection`,
   `stripetest-jwt-signing-key`, `stripetest-stripe-secret-key`,
   `stripetest-stripe-webhook-signing-secret`, `stripetest-starter-price-id`, and
   `stripetest-professional-price-id`.
4. `provision-stripetest.yml` and `deploy-stripetest.yml` are manual-only workflows. They are
   deliberately not run by this change. Their target values must match `target.json` and the
   protected GitHub environment variables before either can proceed.
5. The app workflow validates private Key Vault configuration on a runner with the
   `stripetest-private` label in `stripetest-private-preflight`, builds/pushes images in
   `stripetest-images` only after that preflight succeeds, then deploys in `stripetest`. Register
   separate OIDC federated credentials and required reviewers for all four environments. The
   private runner needs private DNS/network reachability to Key Vault and must be maintained as a
   hardened, preferably ephemeral runner.

The application runtime identity and ACR image-pull identity are separate user-assigned
identities. The runtime identity receives `Key Vault Secrets User` on the dedicated vault; the
image-pull identity receives `AcrPull` scoped to the approved non-production registry. The
ACR assignment is deployed by `acr-pull-role-assignment.bicep` as a module in the registry's
resource group, allowing the main resource-group deployment to assign the role at the separate
registry scope without broadening permissions. Its role-assignment name is derived from the
registry resource ID, image-pull identity name, and role definition ID; the Key Vault assignment
uses the vault resource ID, runtime identity name, and role definition ID. These inputs are known
at deployment start and do not depend on generated principal IDs.
Workflows use separate OIDC identities for provisioning, ACR push, private configuration
preflight, and application deployment. Configure federated credentials and narrowly scoped Azure
RBAC externally. The provisioning identity requires both:

* Permission to create or update ARM nested resource-group deployments in the resource group
  containing the approved non-production ACR. The ACR role-assignment module is deployed there.
* Permission to create the `AcrPull` role assignment scoped to the approved registry.

These are distinct permissions at distinct scopes. Use least-privilege custom roles or
appropriately scoped built-in roles, subject to Azure RBAC validation. Do not grant subscription-wide
`Owner` or `User Access Administrator` solely to satisfy these requirements, and do not grant the
deployment identity general write access to unrelated registries or production resources. Before
provisioning, verify the ACR resource group, exact registry resource ID, and provisioning identity
against the approved target. Both permissions are external Azure prerequisites; this documentation
does not grant either permission.

The ACR-push identity must not have application deployment or Key Vault permissions. The
private-preflight identity is read-only on the dedicated vault and has no deployment or registry
push role. The application deployment identity does not need Key Vault data-plane access or ACR
push permissions; it needs resource-group deployment rights and read access to verify the
image-pull identity's direct ACR role.
The ACR is assumed to be in the approved subscription, although it may be in a separate resource
group.

PostgreSQL has public access disabled and is delegated to its own VNet subnet. The Container Apps
environment and Key Vault private endpoint resolve through private networking. The application
connection string stored in Key Vault must use the exact host/database emitted by the infrastructure
deployment and `Ssl Mode=VerifyFull`. The provisioning administrator credential is separate from
the app login and must never be configured as the runtime connection.

## Public endpoint limitations

Both Container Apps use external HTTPS ingress. All backend routes are externally reachable.
Azure Container Apps external ingress exposes the whole backend application; it does **not** restrict ingress to
`/api/billing/webhook/stripe`. No path-routing gateway, WAF, or API gateway is included.
The apps use Azure Container Apps HTTPS FQDNs; custom DNS names and certificates are not configured.
PostgreSQL and Key Vault ingress are private, but the template does not configure outbound
firewall/NSG restrictions for Container Apps. Review and constrain egress before any broader
integration use; required Stripe API access and platform dependencies need an explicit allowlist
design.

Other routes reachable at the backend endpoint include:

* `GET /api/status` and `GET /health`, `/health/live`, `/health/ready` have no explicit
  authentication requirement in the current code.
* `POST /auth/login`, `/auth/register`, `/auth/refresh`, `/auth/password-reset`,
  `/auth/password-reset/confirm`, and `/auth/email-verification/confirm` are public account
  flows. Authenticated profile/session routes remain protected.
* `POST /api/billing/webhook/stripe` is anonymous by design and depends on signature, timestamp,
  payload-size, API-version, and test-mode validation.
* `POST /webhooks/mpesa/stk` is anonymous and processes provider callbacks. M-PESA outbound
  integration is explicitly disabled in this deployment.
* `GET` and `POST /webhooks/whatsapp` are anonymous webhook routes, but return `404` while
  `WhatsAppCloud:Enabled=false`.
* `POST /api/organization/invitations/reject` is anonymous and token based.
* `POST /api/dev/mpesa/...` and `POST /development/whatsapp/simulate` are anonymous in code but
  return `404` outside the Development host environment.

The remaining business APIs have authentication/authorization policies or API-key requirements in
the current controllers. Keep these properties under review before making the backend publicly
reachable. Do not interpret CORS as an access-control boundary.

## GitHub environment prerequisites

Create `stripetest-infra`, `stripetest-images`, `stripetest-private-preflight`, and `stripetest`
environments and configure required reviewers, branch restrictions, and deployment protections in
GitHub settings. Naming a workflow environment does not itself enforce approval; these settings
have not been verified by this repository change. Register separate OIDC federated credentials
restricted to this repository, the corresponding environment subject, and the expected audience.
The infrastructure identity needs the rights required for the dedicated resource group and the
scoped Key Vault/ACR role assignments. The ACR-push identity needs `AcrPush` and registry
read-only permission on the approved non-production ACR; it must not have application deployment
or Key Vault permissions. The private-preflight identity needs read access to the dedicated
Key Vault only. The app deployment identity needs deployment rights only on the dedicated resource
group, plus narrowly scoped read access for ACR and role-assignment preflight. No identity may
have production access.

The approved ACR must be reachable from the selected build runner for image pushes. If its network
policy excludes GitHub-hosted runners, use an approved private build runner rather than broadening
registry access.

Set common environment variables in the four named environments to the exact values approved in
`target.json`, including subscription, tenant, resource group, region, ACR
name/resource-group/login-server/resource-ID, and database server/host/name:

| GitHub environment variable | Value |
|---|---|
| `STRIPETEST_INFRA_AZURE_CLIENT_ID` | OIDC client ID for infrastructure provisioning |
| `STRIPETEST_ACR_PUSH_AZURE_CLIENT_ID` | OIDC client ID for ACR-push-only image build job |
| `STRIPETEST_PREFLIGHT_AZURE_CLIENT_ID` | OIDC client ID for private, read-only secret preflight |
| `STRIPETEST_DEPLOY_AZURE_CLIENT_ID` | OIDC client ID for application deployment |
| `STRIPETEST_EXPECTED_TENANT_ID` | Reviewed non-production tenant ID |
| `STRIPETEST_EXPECTED_SUBSCRIPTION_ID` | Reviewed non-production subscription ID |
| `STRIPETEST_RESOURCE_GROUP` | Pre-created dedicated StripeTest resource group |
| `STRIPETEST_REGION` | `eastus` |
| `STRIPETEST_ACR_NAME`, `STRIPETEST_ACR_RESOURCE_GROUP` | Approved non-production ACR and its resource group |
| `STRIPETEST_ACR_LOGIN_SERVER`, `STRIPETEST_ACR_RESOURCE_ID` | Exact approved registry endpoint and resource ID |
| `STRIPETEST_POSTGRES_SERVER_NAME`, `STRIPETEST_DATABASE_HOST`, `STRIPETEST_DATABASE_NAME` | Exact dedicated PostgreSQL server/name/FQDN |

Set the exact `STRIPETEST_KEY_VAULT_NAME` value only in `stripetest-private-preflight`. Store
`STRIPETEST_POSTGRES_ADMIN_PASSWORD` only as the `stripetest-infra` environment secret with that
name. Never add Stripe credentials or Price IDs to workflow parameters; populate the six test-vault
secrets only through a separately authorized path that can reach the private vault.

The app workflow's private-preflight job validates all six required secret names, checks the
database connection host/database/`Ssl Mode=VerifyFull`, and checks that the Stripe API credential
has a test-mode key format. It runs on a private self-hosted runner because a GitHub-hosted runner
cannot reach the private-only vault. This job does not expose secret values in logs or job outputs.
The hosted build and deployment jobs validate image references, and the deployment identity checks
actual subscription, resource group, region, ACR resource ID/login server, and the direct `AcrPull`
assignment for the exact dedicated image-pull identity.

Each image is pushed with a unique run tag, but deployment uses the digest emitted by Buildx for
that exact push (`registry/repository@sha256:<64 lowercase hex>`). Backend and frontend references
are independently checked against the exact registry and repository names in `target.json`.
No tag fallback exists. The digest metadata comes from the authenticated push operation; no
separate mutable-tag lookup is used. Provisioning is intentionally a separate earlier phase: it
checks the target and static safety contract but cannot validate runtime Key Vault secrets or app
image digests that do not exist yet. Those checks are required before application deployment.

The app workflow checks for an exact, direct `AcrPull` role assignment before applying the
application template. The assignment is created by the infrastructure deployment in `main.bicep`;
there is no cross-template symbolic dependency. Allow time for Azure RBAC propagation after
provisioning. The application template must not be deployed if the role preflight fails.

## Cost and operations notes

The template uses a small Burstable PostgreSQL SKU, 32 GB initial storage, seven-day local backup
retention, no geo-redundant backup, no dedicated ACR, no NAT gateway, and seven-day Log Analytics
retention. Container Apps scale to zero to reduce idle compute cost. These are planning defaults,
not current Azure price verification. A zero-replica backend may be cold when Stripe sends a
webhook; Stripe retries failed deliveries, so select a test schedule/availability policy before
integration testing. Database storage and backup costs remain while compute is idle.
The design has not been priced against the $300/month planning cap. Private endpoints/DNS,
PostgreSQL uptime and backups, Log Analytics ingestion, and a private self-hosted runner can
materially affect the total. Before provisioning, obtain an approved cost estimate, configure
budget alerts in the subscription, and identify an owner; budget alerts are notifications, not a
hard spending cap.

Schedule test windows with an owner. Outside those windows, scale both apps to zero and stop the
database only through a separately reviewed, supported start/stop procedure; verify webhook
availability and cold-start behavior before enabling test deliveries. Coordinate secret rotation
and removal of synthetic test data when the environment is retired.

The template creates a dedicated Application Insights component and supplies its connection string
to the backend, but the current backend project does not reference an Application Insights SDK
package. Container logs are routed to the dedicated Log Analytics workspace; traces and dependency
telemetry require later application instrumentation work and are not claimed as active here.

Tag resources by application, environment, management method, and reviewed commit SHA. Configure
budget alerts and name an owner for secret rotation, private DNS/network operation, backup/restore
checks, log access, webhook failures, synthetic test-data retention, and cleanup. The report's
historical `$158–$287/month` estimate is not a verified Azure quote.

## Validation

Run the local policy tests without Azure or external services:

```bash
python3 -m unittest discover -s tests/infrastructure -p 'test_*.py'
```

Compile the infrastructure and application templates with Bicep CLI 0.48.1 and `--no-restore`;
keep compiler output artifacts outside the repository. The pinned PostgreSQL API versions may
emit BCP081 because that CLI version has no local resource-type schemas for those versions.
Compilation still validates Bicep syntax and emits templates, but cannot type-check those
PostgreSQL resource properties. Do not run deployment commands as part of offline validation.
