﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

using Raven.Yabt.Database.Models.BacklogItem;

namespace Raven.Yabt.Domain.BacklogItemServices.Commands.DTOs
{
	public abstract class BacklogItemAddUpdRequest
	{
		/// <summary>
		///		The ticket's title
		/// </summary>
		[Required]
		public string Title { get; set; } = null!;

		public string? AssigneeId { get; set; }

		/// <summary>
		///		Related tickets: { Backlog Item ID, Relationship type }.
		/// </summary>
		public IDictionary<string, BacklogItemRelatedItem>? RelatedItems { get; set; }

		/// <summary>
		///		Extra custom properties of various data types configured by the user,
		/// </summary>
		public IDictionary<string, object>? CustomFields { get; set; }
	}
}
