using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleDriveSync
{
    public class FileSyncItem
    {
        public required string Index { get; set; }
        public required string FilePath { get; set; }
        public required string Url { get; set; }
    }
}
