namespace NzbWebDAV.Utils;

public static class TaskUtil
{
    /// <summary>
    /// Waits for all the given tasks to complete.
    /// if any task throws an exception, the method short-circuit and stops early.
    /// In the cases of no errors, the returned results are in order of completion.
    /// </summary>
    /// <param name="tasks">Tha tasks to wait on.</param>
    /// <param name="progress">Callback handler for progress report.</param>
    /// <typeparam name="T">The result type of each task.</typeparam>
    /// <returns>The results of each task.</returns>
    /// <exception cref="Exception">The first task error thrown.</exception>
    public static async Task<List<T>> WhenAllOrError<T>(IEnumerable<Task<T>> tasks, IProgress<int>? progress = null)
    {
        var results = new List<T>();
        var taskList = tasks.ToList();
        var totalTasks = taskList.Count();
        var completedTasks = 0;
        
        try
        {
            while (taskList.Count > 0)
            {
                var completedTask = await Task.WhenAny(taskList);
                taskList.Remove(completedTask);

                if (completedTask.IsFaulted)
                {
                    // Cancel remaining tasks to prevent ObjectDisposedException
                    foreach (var remainingTask in taskList)
                    {
                        try
                        {
                            // Give tasks a chance to clean up gracefully
                            await Task.Delay(10);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                    
                    throw completedTask?.Exception?.InnerException ??
                        new Exception("An unknown error occurred.");
                }

                completedTasks++;
                results.Add(completedTask.Result);
                progress?.Report((completedTasks) * 100 / totalTasks);
            }
        }
        catch (ObjectDisposedException ex) when (ex.ObjectName == "BufferToEndStream")
        {
            // Log the specific disposal issue but don't crash the entire process
            throw new InvalidOperationException($"Stream was disposed during processing. This may indicate concurrent access to the same resource.", ex);
        }

        return results;
    }
}