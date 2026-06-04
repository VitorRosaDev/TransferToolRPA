using System;
using System.Collections.ObjectModel;
using TransferToolRPA.Models;

namespace TransferToolRPA.Services
{
    public interface ICargaQueueService
    {
        ObservableCollection<TransferenciaPayload> Queue { get; }
        void Enqueue(TransferenciaPayload payload);
        bool Dequeue(out TransferenciaPayload? payload);
        void Remove(TransferenciaPayload payload);
        void Clear();
        event Action OnQueueChanged;
    }
}
