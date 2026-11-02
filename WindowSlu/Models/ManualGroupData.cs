using System.Collections.Generic;

namespace WindowSlu.Models
{
    /// <summary>手動グループの永続化データ。</summary>
    public class ManualGroupData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> ProcessNames { get; set; } = new List<string>();
        public List<ManualGroupMemberData> Members { get; set; } = new List<ManualGroupMemberData>();
    }

    /// <summary>手動グループに所属するウィンドウの識別情報。</summary>
    public class ManualGroupMemberData
    {
        public string ProcessName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }
}
