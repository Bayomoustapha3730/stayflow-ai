# Secret Rotation

## Symptoms

- secret expires soon
- a provider starts returning auth failures
- deployment uses a rotated value

## Rotation Steps

1. add the new secret to Key Vault or the deployment secret store
2. update the app reference
3. deploy the new revision
4. verify health and smoke tests
5. retire the old value after stability is confirmed
