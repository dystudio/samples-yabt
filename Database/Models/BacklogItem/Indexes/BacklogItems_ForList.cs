﻿using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Raven.Client.Documents.Indexes;
using Raven.Yabt.Database.Common;

namespace Raven.Yabt.Database.Models.BacklogItem.Indexes
{
	[SuppressMessage("Compiler", "CS8602")] // Suppress "Dereference of a possibly null reference", as Raven handles it on its own
	public class BacklogItems_ForList : AbstractIndexCreationTask<BacklogItem, BacklogItemIndexedForList>
	{
		public BacklogItems_ForList()
		{
			// Add fields that are used for filtering and sorting
			Map = tickets =>
				from ticket in tickets
					let created		= ticket.Modifications.OrderBy(t => t.Timestamp).First()
					let lastUpdated	= ticket.Modifications.OrderBy(t => t.Timestamp).Last()
				select new
				{
					ticket.Title,       // sort
					ticket.Type,        // filter
					AssignedUserId = ticket.Assignee.Id,    // filter

					CreatedByUserId = created.ActionedBy.Id,		// filter
					CreatedTimestamp = created.Timestamp,			// sort
					LastUpdatedTimestamp = lastUpdated.Timestamp,	// sort

					((BacklogItemBug)ticket).Severity,  // sort		Note that 'ticket as BacklogItemBug' would cause a runtime error on building the index
					((BacklogItemBug)ticket).Priority,  // sort

					Search = new[] {
								ticket.Title,
								((BacklogItemBug)ticket).StepsToReproduce,
								((BacklogItemUserStory)ticket).AcceptanceCriteria
							}
							.Concat(ticket.Comments.Select(c => c.Message)),

					// Dynamic fields
					// Notes:
					//	- The format 'collection_key' is required to treat them as dictionary in the C# code
					//	- Prefix is vital, see https://groups.google.com/d/msg/ravendb/YvPZFIn5GVg/907Msqv4CQAJ

					// Create a dictionary for Modifications
					_ = ticket.Modifications.GroupBy(m => m.ActionedBy.Id)                                                           // filter & sort by Timestamp
											.Select(x => CreateField($"{nameof(BacklogItemIndexedForList.ModifiedByUser)}_M{x.Key}", 
																	 x.Max(o => o.Timestamp)
																	 )
													),
					// Create a dictionary for Custom Fields
					__ = ticket.CustomFields
								.Select(x => 
									(LoadDocument<CustomField.CustomField>($"{nameof(CustomField.CustomField)}s/{x.Key}").FieldType == CustomFieldType.Text)
										? CreateField($"{nameof(BacklogItem.CustomFields)}_F{x.Key}", x.Value, false, true)			// search in text Custom Fields
										: CreateField($"{nameof(BacklogItem.CustomFields)}_F{x.Key}", x.Value))						// filter by other Custom Fields (exact match)
				};

			Index(m => m.Search, FieldIndexing.Search);
			Analyzers.Add(x => x.Search, "StandardAnalyzer");
		}
	}
}