using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Models;
using OccultShop.Persistence;

namespace OccultShop.Autoload;

public partial class ShopSessionState : Node
{
	public bool IsShopDayOpen { get; private set; }
	public int ShopDayCustomersArrived { get; private set; }
	public int ShopDayCustomersServed { get; private set; }
	public int ShopDaySuccessfulSales { get; private set; }
	public int ShopDayFailedSales { get; private set; }
	public int ShopDayGoldEarned { get; private set; }
	public int ShopDayDreadChange { get; private set; }
	public bool CloseShopAfterCurrentCustomer { get; private set; }
	public string ActiveCustomerInteractionId { get; private set; } = string.Empty;
	public CustomerRequestDef? ActiveCustomerRequest { get; private set; }

	public event Action? Changed;

	public void ResetSession()
	{
		ResetShopDayState();
		EmitChanged();
	}

	public ShopSessionSnapshot BuildSnapshot()
	{
		return new ShopSessionSnapshot
		{
			IsShopDayOpen = IsShopDayOpen,
			ShopDayCustomersArrived = ShopDayCustomersArrived,
			ShopDayCustomersServed = ShopDayCustomersServed,
			ShopDaySuccessfulSales = ShopDaySuccessfulSales,
			ShopDayFailedSales = ShopDayFailedSales,
			ShopDayGoldEarned = ShopDayGoldEarned,
			ShopDayDreadChange = ShopDayDreadChange,
			CloseShopAfterCurrentCustomer = CloseShopAfterCurrentCustomer,
			ActiveCustomerInteractionId = ActiveCustomerInteractionId,
			ActiveCustomerRequest = CloneCustomerRequest(ActiveCustomerRequest)
		};
	}

	public void ApplySnapshot(ShopSessionSnapshot? snapshot)
	{
		if (snapshot is null)
		{
			GD.PushError("ShopSessionState: Cannot apply a null snapshot.");
			return;
		}

		IsShopDayOpen = snapshot.IsShopDayOpen;
		ShopDayCustomersArrived = Math.Max(0, snapshot.ShopDayCustomersArrived);
		ShopDayCustomersServed = Math.Max(0, snapshot.ShopDayCustomersServed);
		ShopDaySuccessfulSales = Math.Max(0, snapshot.ShopDaySuccessfulSales);
		ShopDayFailedSales = Math.Max(0, snapshot.ShopDayFailedSales);
		ShopDayGoldEarned = snapshot.ShopDayGoldEarned;
		ShopDayDreadChange = snapshot.ShopDayDreadChange;
		CloseShopAfterCurrentCustomer = snapshot.CloseShopAfterCurrentCustomer;
		ActiveCustomerInteractionId = string.IsNullOrWhiteSpace(snapshot.ActiveCustomerInteractionId)
			? string.Empty
			: snapshot.ActiveCustomerInteractionId.Trim();
		ActiveCustomerRequest = CloneCustomerRequest(snapshot.ActiveCustomerRequest);

		if (string.IsNullOrWhiteSpace(ActiveCustomerInteractionId) && ActiveCustomerRequest is not null)
			ActiveCustomerInteractionId = ActiveCustomerRequest.Id.Trim();
		if (!IsShopDayOpen && !string.IsNullOrWhiteSpace(ActiveCustomerInteractionId))
			IsShopDayOpen = true;
		if (IsShopDayOpen && ShopDayCustomersArrived == 0 && !string.IsNullOrWhiteSpace(ActiveCustomerInteractionId))
			ShopDayCustomersArrived = 1;

		EmitChanged();
	}

	public void BeginShopDayState()
	{
		IsShopDayOpen = true;
		ShopDayCustomersArrived = 0;
		ShopDayCustomersServed = 0;
		ShopDaySuccessfulSales = 0;
		ShopDayFailedSales = 0;
		ShopDayGoldEarned = 0;
		ShopDayDreadChange = 0;
		CloseShopAfterCurrentCustomer = false;
		ActiveCustomerInteractionId = string.Empty;
		ActiveCustomerRequest = null;
		EmitChanged();
	}

	public void CloseShopDayState()
	{
		if (!IsShopDayOpen &&
			ShopDayCustomersArrived == 0 &&
			string.IsNullOrWhiteSpace(ActiveCustomerInteractionId) &&
			ActiveCustomerRequest is null)
		{
			return;
		}

		ResetShopDayState();
		EmitChanged();
	}

	public void RecordShopDayCustomerArrived(CustomerInteractionDef interaction)
	{
		if (interaction is null || string.IsNullOrWhiteSpace(interaction.Id))
		{
			GD.PushError("ShopSessionState: Cannot record an active shop customer without an interaction id.");
			return;
		}

		ActiveCustomerInteractionId = interaction.Id.Trim();
		ShopDayCustomersArrived += 1;
		EmitChanged();
	}

	public void ClearActiveShopCustomer()
	{
		if (string.IsNullOrWhiteSpace(ActiveCustomerInteractionId))
			return;

		ActiveCustomerInteractionId = string.Empty;
		EmitChanged();
	}

	public void RecordShopDaySale(bool success, int goldDelta, int dreadDelta)
	{
		ShopDayCustomersServed += 1;
		if (success)
			ShopDaySuccessfulSales += 1;
		else
			ShopDayFailedSales += 1;

		ShopDayGoldEarned += goldDelta;
		ShopDayDreadChange += dreadDelta;
		EmitChanged();
	}

	public void RequestCloseShopAfterCurrentCustomer()
	{
		if (CloseShopAfterCurrentCustomer)
			return;

		CloseShopAfterCurrentCustomer = true;
		EmitChanged();
	}

	public void SetActiveCustomerRequest(CustomerRequestDef? request)
	{
		ActiveCustomerRequest = CloneCustomerRequest(request);
		EnsureActiveShopCustomerForRequest(ActiveCustomerRequest);
		EmitChanged();
	}

	public void ClearActiveCustomerRequest()
	{
		if (ActiveCustomerRequest is null)
			return;

		ActiveCustomerRequest = null;
		EmitChanged();
	}

	private void EmitChanged() => Changed?.Invoke();

	private void ResetShopDayState()
	{
		IsShopDayOpen = false;
		ShopDayCustomersArrived = 0;
		ShopDayCustomersServed = 0;
		ShopDaySuccessfulSales = 0;
		ShopDayFailedSales = 0;
		ShopDayGoldEarned = 0;
		ShopDayDreadChange = 0;
		CloseShopAfterCurrentCustomer = false;
		ActiveCustomerInteractionId = string.Empty;
		ActiveCustomerRequest = null;
	}

	private void EnsureActiveShopCustomerForRequest(CustomerRequestDef? request)
	{
		if (request is null || string.IsNullOrWhiteSpace(request.Id))
			return;

		var requestId = request.Id.Trim();
		IsShopDayOpen = true;
		if (ShopDayCustomersArrived == 0)
			ShopDayCustomersArrived = 1;

		if (!string.Equals(ActiveCustomerInteractionId, requestId, StringComparison.OrdinalIgnoreCase))
			ActiveCustomerInteractionId = requestId;
	}

	private static CustomerRequestDef? CloneCustomerRequest(CustomerRequestDef? request)
	{
		if (request is null)
			return null;

		return new CustomerRequestDef
		{
			Id = request.Id,
			Description = request.Description,
			HideRequestDetails = request.HideRequestDetails,
			DesiredTraits = CustomerTraitRangeDef.CloneDictionary(request.DesiredTraits),
			BadTraits = CustomerTraitRangeDef.CloneDictionary(request.BadTraits),
			RequiredPotionItemId = request.RequiredPotionItemId,
			RequiredPotionDisplayName = request.RequiredPotionDisplayName,
			RequiredMinTraits = request.RequiredMinTraits is null ? new Dictionary<string, int>() : new Dictionary<string, int>(request.RequiredMinTraits),
			RequiredMaxTraits = request.RequiredMaxTraits is null ? new Dictionary<string, int>() : new Dictionary<string, int>(request.RequiredMaxTraits),
			RequiredIngredientAmounts = request.RequiredIngredientAmounts is null
				? new List<IngredientPortionDef>()
				: request.RequiredIngredientAmounts.Select(x => x.Clone()).ToList()
		};
	}
}
