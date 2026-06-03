using System;
using System.IO;
using System.Text.Json;

namespace TransferToolRPA.Models
{
    public record PayloadItem(
        string codigo, 
        double quantidade, 
        string? validade
    );

    public record TransferenciaPayload(
        int id_app,
        string data_geracao,
        string codigo_origem,
        string codigo_destino,
        PayloadItem[] itens
    );

    public static class PayloadValidator
    {
        /// <summary>
        /// Realiza a validação lógica e de segurança do payload JSON com base na estrutura do Mobile.
        /// </summary>
        public static void Validar(TransferenciaPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload), "O payload de dados não pode ser nulo.");

            if (payload.id_app <= 0)
                throw new ArgumentException("O ID da transferência (id_app) é inválido.");

            if (string.IsNullOrWhiteSpace(payload.codigo_origem))
                throw new ArgumentException("O código do depósito de origem (codigo_origem) é obrigatório.");

            if (string.IsNullOrWhiteSpace(payload.codigo_destino))
                throw new ArgumentException("O código da escola de destino (codigo_destino) é obrigatório.");

            if (payload.itens == null || payload.itens.Length == 0)
                throw new ArgumentException("A lista de itens a transferir está vazia.");

            foreach (var item in payload.itens)
            {
                if (string.IsNullOrWhiteSpace(item.codigo))
                    throw new ArgumentException("Existe um item com código de produto ausente ou inválido.");

                if (item.quantidade <= 0)
                    throw new ArgumentException($"A quantidade do produto {item.codigo} deve ser maior que zero (encontrado: {item.quantidade}).");
            }
        }

        /// <summary>
        /// Carrega e valida o payload a partir de um arquivo JSON local.
        /// </summary>
        public static TransferenciaPayload CarregarDeArquivo(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Arquivo de carga JSON não encontrado.", filePath);

            string jsonContent = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var payload = JsonSerializer.Deserialize<TransferenciaPayload>(jsonContent, options);
            if (payload == null)
                throw new InvalidDataException("Falha ao desserializar o arquivo JSON de transferência.");

            Validar(payload);
            return payload;
        }
    }
}
