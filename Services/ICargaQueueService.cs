using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TransferToolRPA.Models;
using TransferToolRPA.ViewModels;

namespace TransferToolRPA.Services
{
    public interface ICargaQueueService
    {
        ObservableCollection<CargaItemViewModel> Queue { get; }
        void Enqueue(TransferenciaPayload payload);
        void EnqueueRange(IEnumerable<TransferenciaPayload> payloads);
        bool Dequeue(out CargaItemViewModel? item);
        void Remove(CargaItemViewModel item);
        void Clear();
        void MoverParaOFim(CargaItemViewModel item);
        event Action OnQueueChanged;
    }
}