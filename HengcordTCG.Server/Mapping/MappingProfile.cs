using AutoMapper;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.DTOs.Auth;

namespace HengcordTCG.Server.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Card, Card>();
        CreateMap<PackType, PackType>();
        
        CreateMap<User, UserInfo>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.DiscordId))
            .ForMember(d => d.IsBotAdmin, opt => opt.MapFrom(s => s.IsBotAdmin));
    }
}
