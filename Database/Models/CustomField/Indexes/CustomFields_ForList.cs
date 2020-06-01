﻿using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Raven.Client.Documents.Indexes;

namespace Raven.Yabt.Database.Models.CustomField.Indexes
{
	[SuppressMessage("Compiler", "CS8602")] // Suppress "Dereference of a possibly null reference", as Raven handles it on its own
	public class CustomFields_ForList : AbstractIndexCreationTask<CustomField, CustomFieldIndexedForList>
	{
		public CustomFields_ForList()
		{
			// Add fields that are used for filtering and sorting
			Map = fields =>
				from field in fields
				select new CustomFieldIndexedForList
				{
					Name = field.Name	// sort & filter
				};
		}
	}
}
