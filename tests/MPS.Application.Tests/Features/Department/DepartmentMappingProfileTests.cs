using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MPS.Application.DTOs;
using MPS.Application.Mappings;
using Xunit;
using Entity = MPS.Domain.Entities.Department;

namespace MPS.Application.Tests.Features.Department;

public sealed class DepartmentMappingProfileTests
{
    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<DepartmentMappingProfile>(),
            NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    [Fact]
    public void Configuration_IsValid()
    {
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<DepartmentMappingProfile>(),
            NullLoggerFactory.Instance);

        config.AssertConfigurationIsValid();
    }

    [Fact]
    public void Maps_Department_To_Dto_IncludingIsActiveAndComputedFields()
    {
        var mapper = CreateMapper();
        var entity = new Entity
        {
            Id = 7,
            Code = "X01",
            Name = "Xưởng khung tranh",
            Description = "Gia công",
            ManagerId = 3,
            Manager = new MPS.Domain.Entities.EmployeeInformation { Id = 3, FullName = "Nguyễn Văn A" },
            IsActive = false,
            Employees =
            [
                new MPS.Domain.Entities.EmployeeInformation { Id = 10 },
                new MPS.Domain.Entities.EmployeeInformation { Id = 11 },
            ],
        };

        var dto = mapper.Map<DepartmentDto>(entity);

        dto.Id.Should().Be(7);
        dto.Code.Should().Be("X01");
        dto.Name.Should().Be("Xưởng khung tranh");
        dto.ManagerId.Should().Be(3);
        dto.ManagerName.Should().Be("Nguyễn Văn A");
        dto.IsActive.Should().BeFalse();
        dto.EmployeeCount.Should().Be(2);
    }

    [Fact]
    public void Maps_NullManager_To_NullManagerName()
    {
        var mapper = CreateMapper();
        var entity = new Entity { Id = 1, Code = "IT", Name = "IT", ManagerId = null, Manager = null, IsActive = true };

        var dto = mapper.Map<DepartmentDto>(entity);

        dto.ManagerName.Should().BeNull();
        dto.EmployeeCount.Should().Be(0);
        dto.IsActive.Should().BeTrue();
    }
}
