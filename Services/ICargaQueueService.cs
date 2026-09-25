using System;
using System.Collections.ObjectModel;
using TransferToolRPA.Models;

namespace TransferToolRPA.Services
{
    public interface ICargaQueueService
    {
        ObservableCollection<TransferenciaPayload> Queue { get; }
        void Enqueue(TransferenciaPayload payload);
        void EnqueueRange(IEnumerable<TransferenciaPayload> payloads);
        bool Dequeue(out TransferenciaPayload? payload);
        void Remove(TransferenciaPayload payload);
        void Clear();
        event Action OnQueueChanged;
    }
}