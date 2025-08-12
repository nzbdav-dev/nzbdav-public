using NzbWebDAV.Queue.FileProcessors;

namespace NzbWebDAV.Queue.FileAggregators;

public interface IAggregator
{
    public void UpdateDatabase(List<BaseProcessor.Result> processorResults);
}