﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ET
{
    internal class MainThreadScheduler: IScheduler
    {
        private readonly ConcurrentQueue<int> idQueue = new();
        private readonly ConcurrentQueue<int> addIds = new();
        private readonly FiberManager fiberManager;
        private readonly ThreadSynchronizationContext threadSynchronizationContext = new();

        public MainThreadScheduler(FiberManager fiberManager)
        {
            SynchronizationContext.SetSynchronizationContext(this.threadSynchronizationContext);
            this.fiberManager = fiberManager;
        }

        public void Dispose()
        {
            this.addIds.Clear();
            this.idQueue.Clear();
        }

        public void Update()
        {
            this.threadSynchronizationContext.Update();
            
            int count = this.idQueue.Count;
            while (count-- > 0)
            {
                if (!this.idQueue.TryDequeue(out int id))
                {
                    continue;
                }

                Fiber fiber = this.fiberManager.Get(id);
                if (fiber == null)
                {
                    continue;
                }
                
                if (fiber.IsDisposed)
                {
                    continue;
                }
                
                fiber.Update();
                
                this.idQueue.Enqueue(id);
            }
        }

        public void LateUpdate()
        {
            int count = this.idQueue.Count;
            while (count-- > 0)
            {
                if (!this.idQueue.TryDequeue(out int id))
                {
                    continue;
                }

                Fiber fiber = this.fiberManager.Get(id);
                if (fiber == null)
                {
                    continue;
                }

                if (fiber.IsDisposed)
                {
                    continue;
                }

                this.idQueue.Enqueue(id);

                fiber.LateUpdate();
            }

            while (this.addIds.Count > 0)
            {
                this.addIds.TryDequeue(out int result);
                this.idQueue.Enqueue(result);
            }
        }


        public void Add(int fiberId = 0)
        {
            this.addIds.Enqueue(fiberId);
        }
    }
}