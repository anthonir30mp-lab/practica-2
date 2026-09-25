using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace practica_2.Services;

/// <summary>
/// Servicio para gestionar el caché distribuido de solicitudes por usuario.
/// Utiliza IDistributedCache (Redis o in-memory como fallback).
/// </summary>
public class SolicitudesCacheService
{
    private readonly IDistributedCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    public SolicitudesCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Genera la cache key para el listado de solicitudes de un usuario.
    /// </summary>
    private static string BuildKey(string userId) => $"solicitudes:listado:{userId}";

    /// <summary>
    /// Intenta obtener el listado cacheado de solicitudes para el usuario dado.
    /// Retorna null si no hay entrada en caché.
    /// </summary>
    public async Task<List<T>?> ObtenerListadoAsync<T>(string userId)
    {
        try
        {
            var bytes = await _cache.GetAsync(BuildKey(userId));
            if (bytes is null || bytes.Length == 0)
                return null;

            return JsonSerializer.Deserialize<List<T>>(bytes);
        }
        catch
        {
            // Si Redis no está disponible, retornar null para que se consulte la BD.
            return null;
        }
    }

    /// <summary>
    /// Almacena el listado de solicitudes en caché por 60 segundos.
    /// </summary>
    public async Task GuardarListadoAsync<T>(string userId, List<T> solicitudes)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(solicitudes);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration
            };

            await _cache.SetAsync(BuildKey(userId), bytes, options);
        }
        catch
        {
            // Si Redis no está disponible, se ignora silenciosamente.
        }
    }

    /// <summary>
    /// Invalida (remueve) la entrada de caché del listado de solicitudes
    /// para el usuario dado. Llamar cuando se crea o actualiza una solicitud.
    /// </summary>
    public async Task InvalidarListadoAsync(string userId)
    {
        try
        {
            await _cache.RemoveAsync(BuildKey(userId));
        }
        catch
        {
            // Si Redis no está disponible, se ignora silenciosamente.
        }
    }
}
