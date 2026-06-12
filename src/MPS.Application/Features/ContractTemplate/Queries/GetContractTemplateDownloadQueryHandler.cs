using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractTemplate.Queries;

public sealed class GetContractTemplateDownloadQueryHandler
    : IRequestHandler<GetContractTemplateDownloadQuery, ContractTemplateDownloadResult>
{
    private readonly IContractTemplateRepository _templateRepository;
    private readonly IFileStorageService _storage;

    public GetContractTemplateDownloadQueryHandler(
        IContractTemplateRepository templateRepository,
        IFileStorageService storage)
    {
        _templateRepository = templateRepository;
        _storage = storage;
    }

    public async Task<ContractTemplateDownloadResult> Handle(
        GetContractTemplateDownloadQuery request,
        CancellationToken cancellationToken)
    {
        var template = await _templateRepository.GetWithFileAsync(request.Id, cancellationToken)
            ?? throw new DomainValidationException($"TEMPLATE_NOT_FOUND:{request.Id}");

        if (template.TemplateFile is null)
            throw new DomainValidationException($"TEMPLATE_FILE_NOT_FOUND:{request.Id}");

        var download = await _storage.DownloadAsync(
            template.TemplateFile.MinioObjectKey,
            cancellationToken: cancellationToken);

        return new ContractTemplateDownloadResult(download.Content, template.TemplateFile.FileName);
    }
}
