using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Windows.Storage.Pickers;

namespace LiteView.Contracts
{
    public interface IFilePickerService
    {
        Task<string?> PickSingleFileAsync(string[] fileTypes, PickerLocationId startLocation = PickerLocationId.DocumentsLibrary);
    }
}
