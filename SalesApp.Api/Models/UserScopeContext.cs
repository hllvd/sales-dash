using System;
using System.Collections.Generic;

namespace SalesApp.Models
{
    public class UserScopeContext
    {
        public bool IsGlobal { get; set; } = false;
        public HashSet<Guid> AllowedUserIds { get; set; } = new HashSet<Guid>();
        public HashSet<string> AllowedMatriculas { get; set; } = new HashSet<string>();
        public HashSet<string> AdminLinkedMatriculas { get; set; } = new HashSet<string>();
        public HashSet<string> AdminOwnedMatriculas { get; set; } = new HashSet<string>();
    }
}
