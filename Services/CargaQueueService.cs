using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TransferToolRPA.Models;
using TransferToolRPA.ViewModels;

namespace TransferToolRPA.Services
{
    public class CargaQueueService : ICargaQueueService
    {
        public ObservableCollection<CargaItemViewModel> Queue { get; } = new();

        public event Action? OnQueueChanged;

        public CargaQueueService()
        {
            Queue.CollectionChanged += (s, e) => OnQueueChanged?.Invoke();
        }

        public void Enqueue(TransferenciaPayload payload)
        {
            Queue.Add(new CargaItemViewModel(payload));
        }

        public void EnqueueRange(IEnumerable<TransferenciaPayload> payloads)
        {
            foreach (var payload in payloads)
            {
                Queue.Add(new CargaItemViewModel(payload));
            }
        }

        public bool Dequeue(out CargaItemViewModel? item)
        {
            if (Queue.Count > 0)
            {
                item = Queue[0];
                Queue.RemoveAt(0);
                return true;
            }

            item = null;
            return false;
        }

        public void Remove(CargaItemViewModel item)
        {
            Queue.Remove(item);
        }

        public void Clear()
        {
            Queue.Clear();
        }

        public void MoverParaOFim(CargaItemViewModel item)
        {
            int index = Queue.IndexOf(item);
            if (index >= 0 && index < Queue.Count - 1)
            {
                Queue.Move(index, Queue.Count - 1);
            }
        }
    }
}