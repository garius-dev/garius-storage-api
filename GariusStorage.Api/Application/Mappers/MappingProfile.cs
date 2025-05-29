using AutoMapper;
using GariusStorage.Api.Application.Dtos.Company;
using GariusStorage.Api.Domain.Entities;

namespace GariusStorage.Api.Application.Mappers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Mapeamentos para Company
            CreateMap<CreateCompanyRequestDto, Company>()
                .ForMember(dest => dest.ImageUrl, opt => opt.Ignore()); // ImageUrl será tratado separadamente

            CreateMap<UpdateCompanyRequestDto, Company>()
                .ForMember(dest => dest.ImageUrl, opt => opt.Ignore()) // ImageUrl será tratado separadamente
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null)); // Para não sobrescrever com nulos do DTO

            CreateMap<Company, CompanyDto>();
        }
    }
}
