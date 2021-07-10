﻿using AutoMapper;
using TradeTracker.Core.DomainModels.Position;
using TradeTracker.Repository.EntityModels.Position;

namespace TradeTracker.Repository.Profiles
{
    public class PositionsProfile : Profile
    {
        public PositionsProfile()
        {
            CreateMap<PositionDomainModel, PositionEntityModel>();

            CreateMap<PositionFilterDomainModel, PositionFilterEntityModel>();
        }
    }
}
