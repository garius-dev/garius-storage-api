using GariusStorage.Api.Configuration;

namespace GariusStorage.Api.Extensions
{
    public static class GeneralExtensions
    {
        public static Dictionary<string,string> FilterKeysStartingWith(this Dictionary<string,string> dict, string startWith)
        {
            var keysToRemove = dict.Keys
                .Where(k => !k.StartsWith(startWith))
                .ToList();

            foreach (var key in keysToRemove)
            {
                dict.Remove(key);
            }

            return dict;
        }

        public static string? GetValueByKey(this UrlCallbackSettings dict, string key)
        {
            key = dict.Environment + "--" + key;

            if (dict.UrlCallbacks.TryGetValue(key, out var value))
            {
                return value;
            }
            return null;
        }
    }
}
