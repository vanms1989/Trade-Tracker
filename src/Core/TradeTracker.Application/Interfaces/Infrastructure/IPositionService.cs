using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradeTracker.Application.Features.Positions;
using TradeTracker.Domain.Entities;
using TradeTracker.Domain.Enums;

namespace TradeTracker.Application.Interfaces.Infrastructure
{
    public interface IPositionService
    { 
        Task AttachToPosition(
            string symbol, 
            TransactionType transactionType, 
            decimal quantity,
            Guid accessKey);

        Task DetachFromPosition(
            string symbol, 
            TransactionType transactionType, 
            decimal quantity,
            Guid accessKey);
        
        Task RecalculateForSymbol(
            string symbol,
            Guid accessKey);

        Task RefreshForTransaction(
            Guid transactionId,
            Guid accessKey);

        Task RefreshForTransactionCollection(
            string symbol,
            List<Guid> transactionIds,
            Guid accessKey);

        Task<decimal> CalculateAverageCostBasis(
            string symbol);

        Task<IEnumerable<SourceTransactionLink>> CreateSourceTransactionMap(
            string symbol);
    }
}