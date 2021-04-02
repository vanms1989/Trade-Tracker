using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradeTracker.Application.ResourceParameters.Paged;
using TradeTracker.Application.ResourceParameters.Unpaged;
using TradeTracker.Domain.Entities;

namespace TradeTracker.Application.Interfaces.Persistence.Transactions
{
    public interface IAuthenticatedTransactionRepository : 
        IAuthenticatedAsyncRepository<Transaction, PagedTransactionsResourceParameters, UnpagedTransactionsResourceParameters>
    {
        Task<IEnumerable<Transaction>> GetTransactionCollectionByIdsAsync(
            IEnumerable<Guid> ids);
    }
}