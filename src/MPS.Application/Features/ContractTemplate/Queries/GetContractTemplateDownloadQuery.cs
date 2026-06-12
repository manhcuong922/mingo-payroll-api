using MediatR;

namespace MPS.Application.Features.ContractTemplate.Queries;

public sealed record GetContractTemplateDownloadQuery(int Id)
    : IRequest<ContractTemplateDownloadResult>;

public sealed record ContractTemplateDownloadResult(Stream DocxStream, string FileName);
