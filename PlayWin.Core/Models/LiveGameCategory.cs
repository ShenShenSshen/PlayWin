using PlayWin.Core.Interface;
using System.Collections.Generic;

namespace PlayWin.Core.Models
{
    public class LiveGameSource
    {
        public ILiveSite Site { get; set; }
        public LiveSubCategory Category { get; set; }
    }
    public class LiveGameCategory
    {
        public string Name { get; set; }
        public string Pic { get; set; }
        public List<LiveGameSource> Sources { get; set; }
    }
}
