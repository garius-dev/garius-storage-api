using GariusStorage.Api.Application.Interfaces;
using GariusStorage.Api.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace GariusStorage.Api.Application.Services
{
    public class TenantResolverService : ITenantResolverService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<TenantResolverService> _logger;

        public TenantResolverService(
            IHttpContextAccessor httpContextAccessor,
            UserManager<ApplicationUser> userManager,
            ILogger<TenantResolverService> logger) // Adicionado ILogger
        {
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
            _logger = logger;
        }

        public Guid? GetCurrentCompanyId()
        {
            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                // 1. Tentar obter CompanyId da Claim (mais eficiente)
                var companyIdClaim = httpContext.User.FindFirstValue("company_id"); // Use a constante para o nome da claim
                if (!string.IsNullOrEmpty(companyIdClaim) && Guid.TryParse(companyIdClaim, out Guid companyIdFromClaim))
                {
                    _logger.LogDebug("CompanyId '{CompanyId}' obtido da claim para o usuário '{UserId}'.", companyIdFromClaim, httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));
                    return companyIdFromClaim;
                }

                // 2. Fallback: Obter CompanyId do ApplicationUser (se a claim não existir ou for inválida)
                // Esta abordagem é menos performática pois envolve uma consulta ao banco.
                // Idealmente, a claim "company_id" deve ser adicionada ao token JWT durante o login.
                var userIdString = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdString))
                {
                    // Em um serviço Scoped/Transient, chamar GetAwaiter().GetResult() pode ser aceitável,
                    // mas em cenários de alta concorrência ou se este método for chamado muitas vezes,
                    // considere refatorar para ser totalmente assíncrono se possível,
                    // ou garantir que o CompanyId seja sempre uma claim.
                    try
                    {
                        var user = _userManager.FindByIdAsync(userIdString).GetAwaiter().GetResult();
                        if (user?.CompanyId != null)
                        {
                            _logger.LogDebug("CompanyId '{CompanyId}' obtido do UserStore para o usuário '{UserId}'. A claim 'company_id' não estava presente ou era inválida.", user.CompanyId.Value, userIdString);
                            return user.CompanyId;
                        }
                        else if (user != null)
                        {
                            _logger.LogWarning("Usuário '{UserId}' autenticado mas não possui CompanyId associado no UserStore.", userIdString);
                        }
                        else
                        {
                            _logger.LogWarning("Usuário com ID '{UserId}' da claim não encontrado no UserStore.", userIdString);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao tentar buscar usuário '{UserId}' para obter CompanyId.", userIdString);
                        // Não re-lançar a exceção para não quebrar a aplicação se o usuário não for encontrado aqui,
                        // mas sim retornar null para que o filtro global possa permitir acesso a dados não-tenantizados
                        // ou para que outras lógicas possam tratar o caso de tenant não resolvido.
                    }
                }
                else
                {
                    _logger.LogWarning("Usuário autenticado mas ClaimTypes.NameIdentifier (UserId) não encontrado.");
                }
            }
            else
            {
                _logger.LogDebug("Usuário não autenticado ou HttpContext/User indisponível. Nenhum CompanyId resolvido.");
            }

            // Outras lógicas para resolver o tenant podem ser adicionadas aqui (ex: subdomínio, header HTTP customizado)
            // if (algumaCondicaoBaseadaEmSubdominio) { return ObterCompanyIdDoSubdominio(); }

            _logger.LogDebug("Nenhum CompanyId pôde ser resolvido para a requisição atual.");
            return null; // Retorna null se não puder determinar (ex: usuário anônimo, superadmin sem company_id, ou erro)
        }
    }
}
