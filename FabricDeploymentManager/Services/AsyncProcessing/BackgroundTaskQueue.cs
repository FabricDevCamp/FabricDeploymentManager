﻿using System.Collections.Concurrent;
using System.Threading.Channels;

namespace FabricDeploymentManager.Services.AsyncProcessing {

  public interface IBackgroundTaskQueue {

    // Enqueues the given task.
    void EnqueueTask(Func<IServiceScopeFactory, CancellationToken, Task> task);

    // Dequeues and returns one task. This method blocks until a task becomes available.
    Task<Func<IServiceScopeFactory, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
  }

  public class BackgroundTaskQueue : IBackgroundTaskQueue {

    private readonly ConcurrentQueue<Func<IServiceScopeFactory, CancellationToken, Task>> _items = new();

    // Holds the current count of tasks in the queue.
    private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

    public void EnqueueTask(Func<IServiceScopeFactory, CancellationToken, Task> task) {
      if (task == null)
        throw new ArgumentNullException(nameof(task));

      _items.Enqueue(task);
      _signal.Release();
    }

    public async Task<Func<IServiceScopeFactory, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken) {
      // Wait for task to become available
      await _signal.WaitAsync(cancellationToken);

      _items.TryDequeue(out var task);
      return task;
    }
  }

}
