using GariusStorage.Api.Configuration;
using Newtonsoft.Json;

namespace GariusStorage.Api.Helpers
{
    public static class LoadConfigHelper
    {
        //Método para ler uma Secret e adiciona-la em um objeto para Option Bind
        public static T LoadConfigFromSecret<T>(IConfiguration configuration, string secretName)
        {
            var secretJson = configuration[secretName] ?? 
                throw new InvalidOperationException($"Segredo '{secretName}' não encontrado ou vazio na configuração.");

            try
            {
                var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                return JsonConvert.DeserializeObject<T>(secretJson, settings)
                       ?? throw new InvalidOperationException($"Falha ao desserializar o JSON do segredo '{secretName}' em {typeof(T).Name}. O JSON desserializado resultou em null.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Erro ao desserializar o JSON do segredo '{secretName}'. Verifique o formato.", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Ocorreu um erro inesperado ao carregar o segredo '{secretName}'.", ex);
            }
        }

    }
}
