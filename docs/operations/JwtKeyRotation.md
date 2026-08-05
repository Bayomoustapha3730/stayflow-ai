# JWT Key Rotation

## Symptoms

- auth tokens begin failing validation
- login or refresh flows return unauthorized errors

## Rotation Steps

1. publish the new signing key
2. update deployment configuration
3. deploy backend revisions with the new key
4. verify token issuance and validation
5. invalidate the old key only after overlap is complete
