using LiteView.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteView.Contracts
{
    public interface IUpdateService
    {
        Task<RemoteVersion?> CheckUpdateAsync();
    }
}
