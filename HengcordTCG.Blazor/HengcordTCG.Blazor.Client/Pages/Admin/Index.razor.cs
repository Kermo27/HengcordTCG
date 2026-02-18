using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using HengcordTCG.Shared.Clients;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.DTOs.Wiki;
using HengcordTCG.Blazor.Client.Services;

namespace HengcordTCG.Blazor.Client.Pages.Admin;

public partial class Index
{
    [Inject]
    private HengcordTCGClient Client { get; set; } = default!;

    [Inject]
    private WikiService WikiService { get; set; } = default!;

    private string _tab = "cards";
    private List<Card> _cards = new();
    private List<PackType> _packs = new();
    private List<User> _users = new();
    private List<WikiProposalListDto> _proposals = new();
    private List<WikiPageDto> _wikiPages = new();
    private string _wikiTab = "pages";

    private bool _showCardModal;
    private bool _showPackModal;
    private bool _showGoldModal;
    private bool _showProposalModal;
    private bool _showRejectModal;
    private bool _showDeleteCardModal;
    private bool _showWikiPageModal;
    private bool _showDeleteWikiPageModal;
    private int? _editingWikiPageId;
    private WikiPageDto? _deletingWikiPage;

    private Card? _editingCard;
    private PackType? _editingPack;
    private User? _editingUser;
    private WikiProposalListDto? _viewingProposal;
    private WikiProposalListDto? _rejectingProposal;
    private Card? _deletingCard;

    private CardFormData _cardForm = new();
    private PackFormData _packForm = new();
    private WikiPageFormData _wikiPageForm = new();
    private int _goldAmount;
    private string _rejectReason = "";

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _cards = await Client.GetCardsAsync();
        _packs = await Client.GetPacksAsync();
        _users = await Client.GetUsersAsync();
        if (_tab == "wiki")
        {
            await LoadProposals();
        }
    }

    private string TabClass(string tab) => _tab == tab
        ? "px-4 py-2 border-b-2 border-amber-500 text-amber-400 font-medium"
        : "px-4 py-2 text-slate-400 hover:text-white transition-colors";

    private void OpenAddCard()
    {
        _editingCard = new Card();
        _cardForm = new CardFormData();
        _showCardModal = true;
    }

    private void OpenEditCard(Card card)
    {
        _editingCard = card;
        _cardForm = new CardFormData
        {
            Name = card.Name,
            CardType = card.CardType.ToString(),
            Rarity = card.Rarity.ToString(),
            Attack = card.Attack,
            Defense = card.Defense,
            Health = card.Health,
            LightCost = card.LightCost,
            Speed = card.Speed,
            ImagePath = card.ImagePath ?? "",
            MinDamage = card.MinDamage,
            MaxDamage = card.MaxDamage,
            CounterStrike = card.CounterStrike,
            AbilityText = card.AbilityText ?? "",
            AbilityId = card.AbilityId ?? "",
            ExclusivePackName = _packs.FirstOrDefault(p => p.Id == card.ExclusivePackId)?.Name ?? ""
        };
        _showCardModal = true;
    }

    private void CloseCardModal()
    {
        _showCardModal = false;
        _editingCard = null;
    }

    private async Task SaveCard()
    {
        if (string.IsNullOrWhiteSpace(_cardForm.Name)) return;

        var card = new Card
        {
            Id = _editingCard?.Id ?? 0,
            Name = _cardForm.Name,
            CardType = Enum.Parse<CardType>(_cardForm.CardType),
            Rarity = Enum.Parse<Rarity>(_cardForm.Rarity),
            Attack = _cardForm.Attack,
            Defense = _cardForm.Defense,
            Health = _cardForm.Health,
            LightCost = _cardForm.LightCost,
            Speed = _cardForm.Speed,
            ImagePath = string.IsNullOrEmpty(_cardForm.ImagePath) ? null : _cardForm.ImagePath,
            MinDamage = _cardForm.MinDamage,
            MaxDamage = _cardForm.MaxDamage,
            CounterStrike = _cardForm.CounterStrike,
            AbilityText = string.IsNullOrEmpty(_cardForm.AbilityText) ? null : _cardForm.AbilityText,
            AbilityId = string.IsNullOrEmpty(_cardForm.AbilityId) ? null : _cardForm.AbilityId
        };

        if (card.Id == 0)
        {
            await Client.AddCardAsync(card);
        }
        else
        {
            await Client.UpdateCardAsync(card);
        }

        if (!string.IsNullOrEmpty(_cardForm.ExclusivePackName))
        {
            await Client.SetCardPackAsync(card.Name, _cardForm.ExclusivePackName);
        }
        else if (_editingCard?.ExclusivePackId != null)
        {
            await Client.SetCardPackAsync(card.Name, "null");
        }

        CloseCardModal();
        await LoadData();
    }

    private async Task HandleCardImageUpload(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null || file.Size == 0) return;

        using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        var path = await Client.UploadImageAsync(stream, file.Name, "cards");
        if (!string.IsNullOrEmpty(path))
        {
            _cardForm.ImagePath = path;
        }
    }

    private void ConfirmDeleteCard(Card card)
    {
        _deletingCard = card;
        _showDeleteCardModal = true;
    }

    private void CloseDeleteCardModal()
    {
        _showDeleteCardModal = false;
        _deletingCard = null;
    }

    private async Task DeleteCard()
    {
        if (_deletingCard != null)
        {
            await Client.RemoveCardAsync(_deletingCard.Name);
            CloseDeleteCardModal();
            await LoadData();
        }
    }

    private void OpenAddPack()
    {
        _editingPack = new PackType();
        _packForm = new PackFormData();
        _showPackModal = true;
    }

    private void OpenEditPack(PackType pack)
    {
        _editingPack = pack;
        _packForm = new PackFormData
        {
            Name = pack.Name,
            Price = pack.Price,
            ChanceCommon = pack.ChanceCommon,
            ChanceRare = pack.ChanceRare,
            ChanceLegendary = pack.ChanceLegendary,
            IsAvailable = pack.IsAvailable
        };
        _showPackModal = true;
    }

    private void ClosePackModal()
    {
        _showPackModal = false;
        _editingPack = null;
    }

    private async Task SavePack()
    {
        if (string.IsNullOrWhiteSpace(_packForm.Name)) return;

        var pack = new PackType
        {
            Id = _editingPack?.Id ?? 0,
            Name = _packForm.Name,
            Price = _packForm.Price,
            ChanceCommon = _packForm.ChanceCommon,
            ChanceRare = _packForm.ChanceRare,
            ChanceLegendary = _packForm.ChanceLegendary,
            IsAvailable = _packForm.IsAvailable
        };

        if (pack.Id == 0)
        {
            await Client.CreatePackAsync(pack);
        }
        else
        {
            await Client.UpdatePackAsync(pack);
        }

        ClosePackModal();
        await LoadData();
    }

    private async Task TogglePack(PackType pack)
    {
        await Client.TogglePackAsync(pack.Name);
        await LoadData();
    }

    private string GetTogglePackClass(PackType pack) => pack.IsAvailable
        ? "bg-red-600 hover:bg-red-700 text-white"
        : "bg-emerald-600 hover:bg-emerald-700 text-white";

    private void OpenEditGold(User user)
    {
        _editingUser = user;
        _goldAmount = (int)user.Gold;
        _showGoldModal = true;
    }

    private void CloseGoldModal()
    {
        _showGoldModal = false;
        _editingUser = null;
    }

    private async Task SetGold(int amount)
    {
        if (_editingUser != null)
        {
            await Client.SetGoldAdminAsync(_editingUser.DiscordId, amount);
            CloseGoldModal();
            await LoadData();
        }
    }

    private async Task AddGold(int amount)
    {
        if (_editingUser != null)
        {
            await Client.GiveGoldAdminAsync(_editingUser.DiscordId, amount);
            CloseGoldModal();
            await LoadData();
        }
    }

    private async Task AddAdmin(User user)
    {
        await Client.AddAdminAsync(user.DiscordId);
        await LoadData();
    }

    private async Task RemoveAdmin(User user)
    {
        await Client.RemoveAdminAsync(user.DiscordId);
        await LoadData();
    }

    private async Task LoadProposals()
    {
        _proposals = await Client.GetWikiProposalsAsync();
    }

    private async Task LoadWikiPages()
    {
        _wikiPages = await WikiService.GetPagesAsync();
    }

    private void ViewProposal(WikiProposalListDto proposal)
    {
        _viewingProposal = proposal;
        _showProposalModal = true;
    }

    private void CloseProposalModal()
    {
        _showProposalModal = false;
        _viewingProposal = null;
    }

    private async Task OpenApproveProposal(WikiProposalListDto proposal)
    {
        await Client.ApproveWikiProposalAsync(proposal.Id);
        await LoadProposals();
    }

    private void OpenRejectProposal(WikiProposalListDto proposal)
    {
        _rejectingProposal = proposal;
        _rejectReason = "";
        _showRejectModal = true;
    }

    private void CloseRejectModal()
    {
        _showRejectModal = false;
        _rejectingProposal = null;
    }

    private async Task RejectProposal()
    {
        if (_rejectingProposal != null)
        {
            await Client.RejectWikiProposalAsync(_rejectingProposal.Id, _rejectReason);
        }
        CloseRejectModal();
        await LoadProposals();
    }

    private void OpenCreateWikiPage()
    {
        _editingWikiPageId = null;
        _wikiPageForm = new WikiPageFormData();
        _showWikiPageModal = true;
    }

    private async Task OpenEditWikiPage(WikiPageDto page)
    {
        _editingWikiPageId = page.Id;
        _wikiPageForm = new WikiPageFormData
        {
            Title = page.Title,
            Slug = page.Slug,
            Content = ""
        };

        var detail = await WikiService.GetPageAsync(page.Slug);
        if (detail != null)
        {
            _wikiPageForm.Content = detail.Content;
        }

        _showWikiPageModal = true;
    }

    private void ConfirmDeleteWikiPage(WikiPageDto page)
    {
        _deletingWikiPage = page;
        _showDeleteWikiPageModal = true;
    }

    private void CloseWikiPageModal()
    {
        _showWikiPageModal = false;
        _editingWikiPageId = null;
    }

    private async Task CreateWikiPage()
    {
        if (string.IsNullOrWhiteSpace(_wikiPageForm.Title) || string.IsNullOrWhiteSpace(_wikiPageForm.Slug))
            return;

        if (_editingWikiPageId != null)
        {
            await WikiService.UpdatePageAsync(_editingWikiPageId.Value, new UpdateWikiPageRequest
            {
                Title = _wikiPageForm.Title,
                Slug = _wikiPageForm.Slug,
                Content = _wikiPageForm.Content
            });
        }
        else
        {
            await WikiService.CreatePageAsync(new CreateWikiPageRequest
            {
                Title = _wikiPageForm.Title,
                Slug = _wikiPageForm.Slug,
                Content = _wikiPageForm.Content
            });
        }

        CloseWikiPageModal();
        await LoadWikiPages();
    }

    private async Task DeleteWikiPage()
    {
        if (_deletingWikiPage != null)
        {
            await WikiService.DeletePageAsync(_deletingWikiPage.Id);
            _showDeleteWikiPageModal = false;
            _deletingWikiPage = null;
            await LoadWikiPages();
        }
    }

    private string GetProposalTypeClass(string type) => type switch
    {
        "NewPage" => "bg-emerald-600/50 text-emerald-300",
        "Edit" => "bg-amber-600/50 text-amber-300",
        "Delete" => "bg-red-600/50 text-red-300",
        _ => "bg-slate-600 text-slate-300"
    };

    private string GetDiffLineClass(string type) => type switch
    {
        "deleted" => "diff-deleted",
        "inserted" => "diff-added",
        "imaginary" => "diff-imaginary",
        "modified" => "diff-modified",
        _ => "diff-unchanged"
    };

    private string GetDiffPrefix(string type) => type switch
    {
        "deleted" => "-",
        "inserted" => "+",
        _ => " "
    };

    private class CardFormData
    {
        public string Name { get; set; } = "";
        public string CardType { get; set; } = "Unit";
        public string Rarity { get; set; } = "Common";
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Health { get; set; }
        public int LightCost { get; set; }
        public int Speed { get; set; }
        public string ImagePath { get; set; } = "";
        public int MinDamage { get; set; }
        public int MaxDamage { get; set; }
        public int CounterStrike { get; set; }
        public string AbilityText { get; set; } = "";
        public string AbilityId { get; set; } = "";
        public string ExclusivePackName { get; set; } = "";
    }

    private class PackFormData
    {
        public string Name { get; set; } = "";
        public int Price { get; set; } = 100;
        public int ChanceCommon { get; set; } = 60;
        public int ChanceRare { get; set; } = 35;
        public int ChanceLegendary { get; set; } = 5;
        public bool IsAvailable { get; set; } = true;
    }

    private class WikiPageFormData
    {
        public string Title { get; set; } = "";
        public string Slug { get; set; } = "";
        public string Content { get; set; } = "";
    }
}
