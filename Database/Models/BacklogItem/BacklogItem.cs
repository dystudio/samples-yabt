﻿using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using Raven.Yabt.Database.Common;
using Raven.Yabt.Database.Common.References;

namespace Raven.Yabt.Database.Models.BacklogItem
{
	/// <summary>
	///		Base class representing common properties accross all types of tickets: bugs, user stories, etc.
	/// </summary>
	/// <remarks>
	///		Can't make the class 'abstract', due to getting exception: Cannot find collection name for abstract class, only concrete class are supported. 
	/// </remarks>
	public class BacklogItem : IEntity
	{
		/// <summary>
		///		The record ID
		/// </summary>
		/// <remarks>
		///		Set by Raven Client. Can be temporarily null before passed to the DocumentSession.Store() method
		/// </remarks>
		public string Id { get; set; } = null!;

		/// <summary>
		///		The Title [mandatory field]
		/// </summary>
		public string Title { get; set; } = null!;

		public virtual BacklogItemType Type { get; set; }    // Can't make it 'abstract'

		/// <summary>
		///		The assigned user to the ticket
		/// </summary>
		public UserReference? Assignee { get; set; }

		/// <summary>
		///		List of all users who modified the ticket.
		///		The first record is creation of the ticket
		/// </summary>
		public IList<BacklogItemHistoryRecord> Modifications { get; } = new List<BacklogItemHistoryRecord>();

		[JsonIgnore]
		public ChangedByUserReference Created		=> Modifications.OrderBy(m => m.Timestamp).FirstOrDefault() as ChangedByUserReference;
		[JsonIgnore]
		public ChangedByUserReference LastUpdated	=> Modifications.OrderBy(m => m.Timestamp).LastOrDefault() as ChangedByUserReference;

		/// <summary>
		///		Related tickets: { Backlog Item ID, Relationship type }.
		/// </summary>
		public IDictionary<string, BacklogItemRelatedItem> RelatedItems { get; set; } = new Dictionary<string, BacklogItemRelatedItem>();

		/// <summary>
		///		Comments on the ticket
		/// </summary>
		public IList<Comment> Comments { get; } = new List<Comment>();

		/// <summary>
		///		Extra custom properties of various data types configured by the user,
		/// </summary>
		public IDictionary<string, object> CustomFields { get; set; } = new Dictionary<string, object>();

		public BacklogItemReference GetReference() => new BacklogItemReference
		{
			Id = Id?.Split('/').Last(),
			Name = Title,
			Type = Type
		};
	}
}