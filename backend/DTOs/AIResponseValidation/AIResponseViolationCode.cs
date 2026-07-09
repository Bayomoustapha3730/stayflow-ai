namespace StayFlow.Api.DTOs.AIResponseValidation;

public enum AIResponseViolationCode
{
    EmptyResponse,
    ResponseTooLong,
    PropertyAccessDisclosure,
    InternalIdentifierDisclosure,
    InternalNotesDisclosure,
    UnsupportedApprovalClaim,
    UnsupportedCompletionClaim,
    PotentialPromptLeakage
}
