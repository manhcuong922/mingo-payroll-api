using MediatR;

namespace MPS.Application.Common.Models.Compliance;

public sealed record ComplianceVersionChangedNotification(string ComplianceType) : INotification;
