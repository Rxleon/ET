﻿using System;
using System.Collections.Generic;
using System.Threading;

namespace ET
{
    public static class FiberHelper
    {
        public static ActorId GetActorId(this Entity self)
        {
            Fiber root = self.Fiber();
            return new ActorId(root.Process, root.Id, self.InstanceId);
        }
    }
    
    public class Fiber: IDisposable
    {
        public bool IsDisposed;
        
        public int Id;

        public int Zone;

        public Scene Root { get; }

        public Address Address
        {
            get
            {
                return new Address(this.Process, this.Id);
            }
        }
        
        public int Process { get; }
        
        public EntitySystem EntitySystem { get; }
        public TimeInfo TimeInfo { get; }
        public IdGenerater IdGenerater { get; private set; }
        public Mailboxes Mailboxes { get; private set; }
        public ThreadSynchronizationContext ThreadSynchronizationContext { get; }
        public TimerComponent TimerComponent { get; }
        public CoroutineLockComponent CoroutineLockComponent { get; }

        private readonly Queue<ETTask> frameFinishTasks = new();
        
        internal Fiber(int id, int process, int zone, SceneType sceneType, string name)
        {
            this.Id = id;
            this.Process = process;
            this.Zone = zone;
            this.EntitySystem = new EntitySystem();
            this.TimeInfo = new TimeInfo();
            this.IdGenerater = new IdGenerater(process, this.TimeInfo);
            this.Mailboxes = new Mailboxes();
            this.TimerComponent = new TimerComponent(this.TimeInfo);
            this.CoroutineLockComponent = new CoroutineLockComponent(this.TimerComponent);
            this.ThreadSynchronizationContext = new ThreadSynchronizationContext();
            this.Root = new Scene(this, id, 1, sceneType, name);
        }

        internal void Update()
        {
            try
            {
                this.ThreadSynchronizationContext.Update();
                this.TimeInfo.Update();
                this.EntitySystem.Update();
                this.TimerComponent.Update();
                this.CoroutineLockComponent.Update();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
        
        internal void LateUpdate()
        {
            try
            {
                this.EntitySystem.LateUpdate();
                FrameFinishUpdate();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public async ETTask WaitFrameFinish()
        {
            ETTask task = ETTask.Create(true);
            this.frameFinishTasks.Enqueue(task);
            await task;
        }

        private void FrameFinishUpdate()
        {
            while (this.frameFinishTasks.Count > 0)
            {
                ETTask task = this.frameFinishTasks.Dequeue();
                task.SetResult();
            }
        }

        public void Dispose()
        {
            if (this.IsDisposed)
            {
                return;
            }
            this.IsDisposed = true;
            
            this.Root.Dispose();
        }
    }
}