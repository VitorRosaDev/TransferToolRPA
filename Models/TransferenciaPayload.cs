using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TransferToolRPA.Models
{
    public record PayloadItemEntrada(
        string[] codigos,
        double quantidade
    );

    public record TransferenciaPayload(
        int id_app,
        string data_geracao,
        string codigo_origem,
        string codigo_destino,
        PayloadItemEntrada[] itens
    );

    public record ItemTransferenciaInterno(
        string Codigo,
        double Quantidade
    );

    public static class PayloadValidator
    {
        public static void Validar(TransferenciaPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload), "O payload de dados não pode ser nulo.");

            if (payload.id_app <= 0)
                throw new ArgumentException("O ID da transferência (id_app) é inválido.");

            if (string.IsNullOrWhiteSpace(payload.codigo_origem))
                throw new ArgumentException("O código do depósito de origem (codigo_origem) é obrigatório.");
            if (payload.codigo_origem.Length > 50 || ContemCaracteresSuspeitos(payload.codigo_origem))
                throw new ArgumentException("O código do depósito de origem (codigo_origem) excede o tamanho permitido ou contém caracteres inválidos.");

            if (string.IsNullOrWhiteSpace(payload.codigo_destino))
                throw new ArgumentException("O código da escola de destino (codigo_destino) é obrigatório.");
            if (payload.codigo_destino.Length > 50 || ContemCaracteresSuspeitos(payload.codigo_destino))
                throw new ArgumentException("O código da escola de destino (codigo_destino) excede o tamanho permitido ou contém caracteres inválidos.");

            if (payload.itens == null || payload.itens.Length == 0)
                throw new ArgumentException("A lista de itens a transferir está vazia.");

            foreach (var item in payload.itens)
            {
                if (item.codigos == null || item.codigos.Length == 0)
                    throw new ArgumentException("Existe um item com código de produto ausente ou inválido.");

                if (item.codigos.Length > 1)
                {
                    System.Diagnostics.Debug.WriteLine($"[AVISO] Item com múltiplos códigos detectado (legacy): {string.Join(", ", item.codigos)}. Será usado apenas o primeiro.");
                }

                string codigo = item.codigos[0];
                if (string.IsNullOrWhiteSpace(codigo))
                    throw new ArgumentException("Existe um item com código de produto vazio.");
                if (codigo.Length > 50 || ContemCaracteresSuspeitos(codigo))
                    throw new ArgumentException($"O código do produto '{codigo}' excede o tamanho permitido ou contém caracteres inválidos.");

                if (item.quantidade <= 0)
                    throw new ArgumentException($"A quantidade do produto {codigo} deve ser maior que zero (encontrado: {item.quantidade}).");
                if (item.quantidade > 1000000)
                    throw new ArgumentException($"A quantidade do produto {codigo} excede o limite máximo de segurança de 1.000.000 unidades.");
            }
        }

        public static void ValidarTodas(IEnumerable<TransferenciaPayload> payloads)
        {
            if (payloads == null)
                throw new ArgumentNullException(nameof(payloads), "A lista de payloads não pode ser nula.");

            var payloadArray = payloads.ToArray();
            if (payloadArray.Length == 0)
                throw new ArgumentException("Nenhuma transferência encontrada no arquivo.");

            foreach (var payload in payloadArray)
            {
                Validar(payload);
            }
        }

        private static bool ContemCaracteresSuspeitos(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            string[] suspeitos = { "..", "/", "\\", ";", "'", "\"", "<", ">", "\n", "\r" };
            foreach (var s in suspeitos)
            {
                if (input.Contains(s)) return true;
            }
            return false;
        }

        public static TransferenciaPayload[] CarregarDeArquivo(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Arquivo de carga JSON não encontrado.", filePath);

            string jsonContent = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var payloads = JsonSerializer.Deserialize<TransferenciaPayload[]>(jsonContent, options);
            if (payloads == null || payloads.Length == 0)
                throw new InvalidDataException("Falha ao desserializar o arquivo JSON de transferência ou arquivo vazio.");

            ValidarTodas(payloads);
            return payloads;
        }

        public static IEnumerable<ItemTransferenciaInterno> ExpandirItens(TransferenciaPayload payload)
        {
            if (payload?.itens == null) yield break;

            foreach (var item in payload.itens)
            {
                string codigo = item.codigos[0];
                yield return new ItemTransferenciaInterno(codigo, item.quantidade);
            }
        }
    }
}