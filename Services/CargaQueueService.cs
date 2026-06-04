using System;
using System.Collections.ObjectModel;
using TransferToolRPA.Models;

namespace TransferToolRPA.Services
{
    public class CargaQueueService : ICargaQueueService
    {
        public ObservableCollection<TransferenciaPayload> Queue { get; } = new();

        public event Action? OnQueueChanged;

        public CargaQueueService()
        {
            Queue.CollectionChanged += (s, e) => OnQueueChanged?.Invoke();
        }

        public void Enqueue(TransferenciaPayload payload)
        {
            Queue.Add(payload);
        }

        public bool Dequeue(out TransferenciaPayload? payload)
        {
            if (Queue.Count > 0)
            {
                payload = Queue[0];
                Queue.RemoveAt(0);
                return true;
            }
            payload = null;
            return false;
        }

        public void Remove(TransferenciaPayload payload)
        {
            Queue.Remove(payload);
        }

        public void Clear()
        {
            Queue.Clear();
        }
    }
}
