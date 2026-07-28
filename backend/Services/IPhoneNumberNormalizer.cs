namespace StayFlow.Api.Services;

public interface IPhoneNumberNormalizer
{
    bool TryNormalize(string? value, out string normalized);
    string? Mask(string? value);
}