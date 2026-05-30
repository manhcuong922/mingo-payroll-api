using MPS.Application.Common.Models;
using MPS.Domain.Entities;

namespace MPS.Application.Common.Interfaces;

public interface IJwtTokenService
{
    AccessTokenResult GenerateAccessToken(User user, IEnumerable<string> permissionCodes);
}
