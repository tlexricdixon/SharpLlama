using LLama;
using LLama.Common;

namespace Contracts;

public interface ILLamaWeightManager : IDisposable
{
    LLamaWeights GetOrCreateWeights(string modelPath, ModelParams parameters);
    void RemoveWeights(string modelPath);
    int GetLoadedWeightsCount();
}