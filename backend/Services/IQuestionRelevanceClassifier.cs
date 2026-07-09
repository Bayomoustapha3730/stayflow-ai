using StayFlow.Api.DTOs.AIContext;

namespace StayFlow.Api.Services;

public interface IQuestionRelevanceClassifier
{
    IReadOnlyCollection<QuestionContextCategory> Classify(string question);
}
