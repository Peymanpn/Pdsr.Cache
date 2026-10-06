namespace Pdsr.Cache.Internal;

internal static class TaskExtensions
{
    /// <summary>
    /// Stops waiting for <paramref name="task"/> when <paramref name="cancellationToken"/> fires.
    /// The task itself keeps running: StackExchange.Redis commands can't be cancelled once sent, so a cancelled
    /// write may still be applied. A later fault of the abandoned task is observed so it isn't reported as unhandled.
    /// </summary>
    public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            return await task.ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled))
        {
            if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task)
            {
                Observe(task);
                throw new OperationCanceledException(cancellationToken);
            }
        }
        return await task.ConfigureAwait(false);
    }

    /// <inheritdoc cref="WithCancellation{T}(Task{T}, CancellationToken)"/>
    public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled))
        {
            if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task)
            {
                Observe(task);
                throw new OperationCanceledException(cancellationToken);
            }
        }
        await task.ConfigureAwait(false);
    }

    // An abandoned task that later faults must not surface as an unobserved exception.
    private static void Observe(Task task)
        => task.ContinueWith(static t => _ = t.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
