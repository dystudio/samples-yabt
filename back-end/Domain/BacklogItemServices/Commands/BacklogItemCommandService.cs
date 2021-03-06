using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DomainResults.Common;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;
using Raven.Yabt.Database.Common;
using Raven.Yabt.Database.Common.References;
using Raven.Yabt.Database.Models.BacklogItems;
using Raven.Yabt.Database.Models.BacklogItems.Indexes;
using Raven.Yabt.Domain.BacklogItemServices.Commands.DTOs;
using Raven.Yabt.Domain.Common;
using Raven.Yabt.Domain.CustomFieldServices.Query;
using Raven.Yabt.Domain.Helpers;
using Raven.Yabt.Domain.UserServices.Query;

namespace Raven.Yabt.Domain.BacklogItemServices.Commands
{
	public class BacklogItemCommandService : BaseService<BacklogItem>, IBacklogItemCommandService
	{
		private readonly IUserReferenceResolver _userResolver;
		private readonly ICustomFieldQueryService _customFieldQueryService;

		public BacklogItemCommandService(IAsyncDocumentSession dbSession, IUserReferenceResolver userResolver, ICustomFieldQueryService customFieldQueryService) : base(dbSession)
		{
			_userResolver = userResolver;
			_customFieldQueryService = customFieldQueryService;
		}

		public async Task<IDomainResult<BacklogItemReference>> Create<T>(T dto) where T : BacklogItemAddUpdRequestBase
		{
			BacklogItem? ticket = dto switch
			{
				BugAddUpdRequest bug		 => await ConvertDtoToEntity<BacklogItemBug, BugAddUpdRequest>(bug),
				UserStoryAddUpdRequest story => await ConvertDtoToEntity<BacklogItemUserStory, UserStoryAddUpdRequest>(story),
				_ => null,
			};
			if (ticket == null)
				return DomainResult.Failed<BacklogItemReference>("Incorrect Backlog structure");

			await DbSession.StoreAsync(ticket);

			return DomainResult.Success(
									ticket.ToReference().RemoveEntityPrefixFromId()
								);
		}

		public async Task<IDomainResult<BacklogItemReference>> Delete(string id)
		{
			var ticket = await DbSession.LoadAsync<BacklogItem>(GetFullId(id));
			if (ticket == null)
				return DomainResult.NotFound<BacklogItemReference>();

			DbSession.Delete(ticket);

			return DomainResult.Success(
									ticket.ToReference().RemoveEntityPrefixFromId()
								);
		}

		public async Task<IDomainResult<BacklogItemReference>> Update<T>(string id, T dto) where T : BacklogItemAddUpdRequestBase
		{
			var entity = await DbSession.LoadAsync<BacklogItem>(GetFullId(id));
			if (entity == null)
				return DomainResult.NotFound<BacklogItemReference>();

			entity = dto switch
			{
				BugAddUpdRequest bug			=> await ConvertDtoToEntity (bug,	entity as BacklogItemBug),
				UserStoryAddUpdRequest story	=> await ConvertDtoToEntity (story,	entity as BacklogItemUserStory),
				_ => null
			};
			if (entity == null)
				return DomainResult.Failed<BacklogItemReference>("Incorrect Backlog structure");

			return DomainResult.Success(
									entity.ToReference().RemoveEntityPrefixFromId()
								);
		}

		public async Task<IDomainResult<BacklogItemReference>> AssignToUser(string backlogItemId, string? userShortenId)
		{
			var backlogItem = await DbSession.LoadAsync<BacklogItem>(GetFullId(backlogItemId));
			if (backlogItem == null)
				return DomainResult.NotFound<BacklogItemReference>("The Backlog Item not found");

			if (userShortenId == null)
			{
				backlogItem.Assignee = null;
			}
			else
			{
				var userRef = await _userResolver.GetReferenceById(userShortenId);
				if (userRef == null)
					return DomainResult.NotFound<BacklogItemReference>("The user not found");

				backlogItem.Assignee = userRef;
			}

			backlogItem.AddHistoryRecord(await _userResolver.GetCurrentUserReference(), "Assigned a user");

			return DomainResult.Success(
									backlogItem.ToReference().RemoveEntityPrefixFromId()
								);
		}

		private async Task<TModel> ConvertDtoToEntity<TModel, TDto>(TDto dto, TModel? entity = null)
			where TModel : BacklogItem, new()
			where TDto : BacklogItemAddUpdRequestBase
		{
			entity ??= new TModel();

			entity.Title = dto.Title;
			entity.Tags = dto.Tags;
			entity.Assignee = dto.AssigneeId != null ? await _userResolver.GetReferenceById(dto.AssigneeId) : null;
	
			entity.AddHistoryRecord(
				await _userResolver.GetCurrentUserReference(), 
				entity.ModifiedBy.Any() ? "Modified" : "Created"	// TODO: Provide more informative description in case of modifications
				);

			if (dto.CustomFields != null)
			{
				var verifiedCustomFieldIds = await _customFieldQueryService.GetFullIdsOfExistingItems(
						dto.CustomFields.Where(pair => pair.Value != null).Select(pair => pair.Key)
					);
				entity.CustomFields = verifiedCustomFieldIds.ToDictionary(x => x.Value, x => dto.CustomFields[x.Key]!);
			}
			else
				entity.CustomFields = null;

			entity.RelatedItems = dto.RelatedItems != null ? await ResolveRelatedItems(dto.RelatedItems) : null;

			// entity.CustomProperties = dto.CustomProperties;	TODO: De-serialise custom properties

			if (dto is BugAddUpdRequest bugDto && entity is BacklogItemBug bugEntity)
			{
				bugEntity.Severity = bugDto.Severity;
				bugEntity.Priority = bugDto.Priority;
				bugEntity.StepsToReproduce = bugDto.StepsToReproduce;
				bugEntity.AcceptanceCriteria = bugDto.AcceptanceCriteria;
			}
			else if (dto is UserStoryAddUpdRequest storyDto && entity is BacklogItemUserStory storyEntity)
			{
				storyEntity.AcceptanceCriteria = storyDto.AcceptanceCriteria;
			}

			return entity;
		}

		private async Task<IList<BacklogItemRelatedItem>?> ResolveRelatedItems(IDictionary<string, BacklogRelationshipType>? relatedItems)
		{
			if (relatedItems == null)
				return null;

			var ids = relatedItems.Keys.Select(GetFullId);

			var references = await (from b in DbSession.Query<BacklogItemIndexedForList, BacklogItems_ForList>()
									where b.Id.In(ids)
									select new BacklogItemReference
									{
										Id = b.Id,
										Name = b.Title,
										Type = b.Type
									}).ToListAsync();

			return (from r in references
					select new BacklogItemRelatedItem
					{
						RelatedTo = r,
						LinkType = relatedItems[r.Id!]
					}).ToList();
		}
	}
}
