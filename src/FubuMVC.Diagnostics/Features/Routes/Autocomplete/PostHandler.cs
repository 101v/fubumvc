using System;
using System.Collections.Generic;
using System.Linq;
using FubuMVC.Core.Registration;
using FubuMVC.Core.Registration.Nodes;
using FubuMVC.Diagnostics.Core.Grids;
using FubuMVC.Diagnostics.Models;
using FubuMVC.Diagnostics.Models.Grids;

namespace FubuMVC.Diagnostics.Features.Routes.Autocomplete
{
    public class PostHandler
    {
        private readonly BehaviorGraph _behaviorGraph;
        private readonly IGridService<BehaviorGraph, BehaviorChain> _gridService;

        public PostHandler(BehaviorGraph behaviorGraph, IGridService<BehaviorGraph, BehaviorChain> gridService)
        {
            _behaviorGraph = behaviorGraph;
            _gridService = gridService;
        }

        public JsonAutocompleteResultModel Execute(AutocompleteRequestModel<BehaviorGraph> request)
        {
            var filter = new JsonGridFilter {ColumnName = request.Column, Values = new List<string> {request.Query}};
        	var query = JsonGridQuery.ForFilter(filter);
            return new JsonAutocompleteResultModel
                       {
                           Values = _gridService
                               .GridFor(_behaviorGraph, query)
                               .Rows
                               .SelectMany(r => r.Columns.Where(c => c.Name.Equals(request.Column, StringComparison.OrdinalIgnoreCase)))
                               .Distinct()
                       };
        }
    }
}