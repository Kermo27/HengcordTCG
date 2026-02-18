using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using HengcordTCG.Shared.Clients;
using HengcordTCG.Shared.Models;
using HengcordTCG.Blazor.Client.Services;

namespace HengcordTCG.Blazor.Client.Pages;

[Authorize]
public partial class Deck
{
    [Inject]
    private HengcordTCGClient Client { get; set; } = default!;

    [Inject]
    private AuthService AuthService { get; set; } = default!;

    private Card? Commander { get; set; }
    private List<Card> MainDeck { get; set; } = new();
    private List<Card> CloserDeck { get; set; } = new();

    private List<UserCard> Collection { get; set; } = new();
    private string Message = "";
    private string MessageClass = "";

    private bool ShowCardPicker;
    private DeckSection CurrentPickerSection;
    private int PickerSlotIndex;
    private string CardSearchQuery = "";

    private bool IsValid => Commander != null && MainDeck.Count == 9 && CloserDeck.Count == 3;

    private string PickerTitle => CurrentPickerSection switch
    {
        DeckSection.Commander => "Commander",
        DeckSection.MainDeck => "Main Deck Card",
        DeckSection.Closer => "Closer Card",
        _ => "Card"
    };

    private string PickerSectionTitle => CurrentPickerSection switch
    {
        DeckSection.Commander => "Commander (CardType.Commander)",
        DeckSection.MainDeck => "Unit Card (CardType.Unit)",
        DeckSection.Closer => "Closer Card (CardType.Closer)",
        _ => "Card"
    };

    private List<Card> FilteredCollectionCards
    {
        get
        {
            var requiredType = CurrentPickerSection switch
            {
                DeckSection.Commander => CardType.Commander,
                DeckSection.MainDeck => CardType.Unit,
                DeckSection.Closer => CardType.Closer,
                _ => (CardType?)null
            };

            var query = Collection
                .Where(uc => uc.Count > 0)
                .Select(uc => uc.Card)
                .Where(c => requiredType == null || c.CardType == requiredType)
                .Where(c => string.IsNullOrEmpty(CardSearchQuery) || c.Name.Contains(CardSearchQuery, StringComparison.OrdinalIgnoreCase));

            if (CurrentPickerSection == DeckSection.MainDeck)
            {
                var usedIds = MainDeck.Select(c => c.Id).ToList();
                query = query.Where(c => !usedIds.Contains(c.Id));
            }
            else if (CurrentPickerSection == DeckSection.Closer)
            {
                var usedIds = CloserDeck.Select(c => c.Id).ToList();
                query = query.Where(c => !usedIds.Contains(c.Id));
            }

            return query.ToList();
        }
    }

    private enum DeckSection
    {
        Commander,
        MainDeck,
        Closer
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadCollection();
        await LoadDeck();
    }

    private async Task LoadCollection()
    {
        var user = await AuthService.GetCurrentUserAsync();
        if (user != null)
        {
            Collection = await Client.GetCollectionAsync(user.Id);
        }
    }

    private async Task LoadDeck()
    {
        Message = "";
        var user = await AuthService.GetCurrentUserAsync();
        if (user != null)
        {
            var deck = await Client.GetDeckAsync(user.Id);
            if (deck != null)
            {
                Commander = deck.Commander;
                MainDeck = deck.DeckCards
                    .Where(dc => dc.Slot == DeckSlot.MainDeck)
                    .Select(dc => dc.Card)
                    .ToList();
                CloserDeck = deck.DeckCards
                    .Where(dc => dc.Slot == DeckSlot.Closer)
                    .Select(dc => dc.Card)
                    .ToList();
            }
            else
            {
                Commander = null;
                MainDeck = new List<Card>();
                CloserDeck = new List<Card>();
            }
        }
    }

    private async Task SaveDeck()
    {
        if (!IsValid)
        {
            Message = "Please fill all deck slots before saving.";
            MessageClass = "bg-red-500/20 border border-red-500/50 text-red-400";
            return;
        }

        var user = await AuthService.GetCurrentUserAsync();
        if (user == null) return;

        var request = new HengcordTCGClient.SaveDeckRequest(
            user.Id,
            null,
            Commander!.Id,
            MainDeck.Select(c => c.Id).ToList(),
            CloserDeck.Select(c => c.Id).ToList()
        );

        var result = await Client.SaveDeckAsync(request);

        if (result.Success)
        {
            Message = result.Message;
            MessageClass = "bg-green-500/20 border border-green-500/50 text-green-400";
        }
        else
        {
            Message = result.Message;
            MessageClass = "bg-red-500/20 border border-red-500/50 text-red-400";
        }
    }

    private void OpenCardPicker(DeckSection section, int slotIndex = 0)
    {
        CurrentPickerSection = section;
        PickerSlotIndex = slotIndex;
        CardSearchQuery = "";
        ShowCardPicker = true;
    }

    private void CloseCardPicker()
    {
        ShowCardPicker = false;
    }

    private void SelectCard(Card card)
    {
        switch (CurrentPickerSection)
        {
            case DeckSection.Commander:
                Commander = card;
                break;
            case DeckSection.MainDeck:
                if (PickerSlotIndex < MainDeck.Count)
                    MainDeck[PickerSlotIndex] = card;
                else
                    MainDeck.Add(card);
                break;
            case DeckSection.Closer:
                if (PickerSlotIndex < CloserDeck.Count)
                    CloserDeck[PickerSlotIndex] = card;
                else
                    CloserDeck.Add(card);
                break;
        }
        CloseCardPicker();
    }

    private void RemoveCommander()
    {
        Commander = null;
    }

    private void RemoveFromMainDeck(int index)
    {
        if (index >= 0 && index < MainDeck.Count)
        {
            MainDeck.RemoveAt(index);
        }
    }

    private void RemoveFromCloserDeck(int index)
    {
        if (index >= 0 && index < CloserDeck.Count)
        {
            CloserDeck.RemoveAt(index);
        }
    }
}
