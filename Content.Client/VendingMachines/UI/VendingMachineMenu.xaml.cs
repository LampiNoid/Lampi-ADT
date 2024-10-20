using System.Numerics;
using Content.Shared.VendingMachines;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using FancyWindow = Content.Client.UserInterface.Controls.FancyWindow;
using Robust.Client.UserInterface;
using Content.Client.UserInterface.Controls;
using Content.Shared.IdentityManagement;
using Robust.Client.Graphics;

namespace Content.Client.VendingMachines.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class VendingMachineMenu : FancyWindow
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;

        private readonly Dictionary<EntProtoId, EntityUid> _dummies = [];

        public event Action<GUIBoundKeyEventArgs, ListData>? OnItemSelected;

        private readonly StyleBoxFlat _styleBox = new() { BackgroundColor = new Color(70, 73, 102) };
        public Action<VendingMachineWithdrawMessage>? OnWithdraw; //ADT-Economy

        public VendingMachineMenu()
        {
            MinSize = new Vector2(250, 150); // Corvax-Resize
            SetSize = new Vector2(450, 150); // Corvax-Resize
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);

            VendingContents.SearchBar = SearchBar;
            VendingContents.DataFilterCondition += DataFilterCondition;
            VendingContents.GenerateItem += GenerateButton;
            VendingContents.ItemKeyBindDown += (args, data) => OnItemSelected?.Invoke(args, data);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            // Don't clean up dummies during disposal or we'll just have to spawn them again
            if (!disposing)
                return;

            // Delete any dummy items we spawned
            foreach (var entity in _dummies.Values)
            {
                _entityManager.QueueDeleteEntity(entity);
            }
            _dummies.Clear();
        }

        private bool DataFilterCondition(string filter, ListData data)
        {
            if (data is not VendorItemsListData { ItemText: var text })
                return false;

            if (string.IsNullOrEmpty(filter))
                return true;

            return text.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
        }

        private void GenerateButton(ListData data, ListContainerButton button)
        {
            if (data is not VendorItemsListData { ItemProtoID: var protoID, ItemText: var text })
                return;

            button.AddChild(new VendingMachineItem(protoID, text));

            button.ToolTip = text;
            button.StyleBoxOverride = _styleBox;
        }

        /// <summary>
        /// Populates the list of available items on the vending machine interface
        /// and sets icons based on their prototypes
        /// </summary>
        public void Populate(List<VendingMachineInventoryEntry> inventory)
        {
            if (inventory.Count == 0 && VendingContents.Visible)
            {
                SearchBar.Visible = false;
                VendingContents.Visible = false;

                var outOfStockLabel = new Label()
                {
                    Text = Loc.GetString("vending-machine-component-try-eject-out-of-stock"),
                    Margin = new Thickness(4, 4),
                    HorizontalExpand = true,
                    VerticalAlignment = VAlignment.Stretch,
                    HorizontalAlignment = HAlignment.Center
                };

                MainContainer.AddChild(outOfStockLabel);

                SetSizeAfterUpdate(outOfStockLabel.Text.Length, 0);

                return;
            }

            var longestEntry = string.Empty;
            var listData = new List<VendorItemsListData>();

            for (var i = 0; i < inventory.Count; i++)
            {
                var entry = inventory[i];

                if (!_prototypeManager.TryIndex(entry.ID, out var prototype))
                    continue;

                if (!_dummies.TryGetValue(entry.ID, out var dummy))
                {
                    dummy = _entityManager.Spawn(entry.ID);
                    _dummies.Add(entry.ID, dummy);
                }

                var itemName = Identity.Name(dummy, _entityManager);
                var itemText = $"{itemName} [{entry.Amount}]";

                if (itemText.Length > longestEntry.Length)
                    longestEntry = itemText;

                listData.Add(new VendorItemsListData(prototype.ID, itemText, i));
            }

            VendingContents.PopulateList(listData);

            SetSizeAfterUpdate(longestEntry.Length, inventory.Count);
        }

        /// <summary>
        /// Populates the list of available items on the vending machine interface
        /// and sets icons based on their prototypes
        /// </summary>
        public void Populate(EntityUid entityUid, List<VendingMachineInventoryEntry> inventory, double priceMultiplier, int credits)
        {
            //ADT-Economy-Start
            CreditsLabel.Text = Loc.GetString("vending-ui-credits-amount", ("credits", credits));
            WithdrawButton.Disabled = credits == 0;
            WithdrawButton.OnPressed += _ =>
            {
                if (credits == 0)
                    return;
                OnWithdraw?.Invoke(new VendingMachineWithdrawMessage());
            };
            var vendComp = _entityManager.GetComponent<VendingMachineComponent>(entityUid); //ADT-Economy
            //ADT-Economy-End

            if (inventory.Count == 0 && VendingContents.Visible)
            {
                SearchBar.Visible = false;
                VendingContents.Visible = false;

                var outOfStockLabel = new Label()
                {
                    Text = Loc.GetString("vending-machine-component-try-eject-out-of-stock"),
                    Margin = new Thickness(4, 4),
                    HorizontalExpand = true,
                    VerticalAlignment = VAlignment.Stretch,
                    HorizontalAlignment = HAlignment.Center
                };

                MainContainer.AddChild(outOfStockLabel);

                SetSizeAfterUpdate(outOfStockLabel.Text.Length, 0);

                return;
            }

            var longestEntry = string.Empty;
            var listData = new List<VendorItemsListData>();

            for (var i = 0; i < inventory.Count; i++)
            {
                var entry = inventory[i];

                if (!_prototypeManager.TryIndex(entry.ID, out var prototype))
                    continue;

                //ADT-Economy-Start
                var price = 0;
                if (!vendComp.AllForFree)
                {
                    price = (int)(entry.Price * priceMultiplier);
                }
                else
                {
                    price = 0; // Это работает только если заспавненный вендомат уже был с этим значением. Спасибо визардам и их bounduserinterface емае.
                }
                //ADT-Economy-Start

                if (!_dummies.TryGetValue(entry.ID, out var dummy))
                {
                    dummy = _entityManager.Spawn(entry.ID);
                    _dummies.Add(entry.ID, dummy);
                }

                var itemName = Identity.Name(dummy, _entityManager);
                var itemText = $" [{price}$] {itemName} [{entry.Amount}]"; //ADT-Economy

                if (itemText.Length > longestEntry.Length)
                    longestEntry = itemText;

                listData.Add(new VendorItemsListData(prototype.ID, itemText, i));
            }

            VendingContents.PopulateList(listData);

            SetSizeAfterUpdate(longestEntry.Length, inventory.Count);
        }

        private void SetSizeAfterUpdate(int longestEntryLength, int contentCount)
        {
            SetSize = new Vector2(Math.Clamp((longestEntryLength + 2) * 12, 250, 400),
                Math.Clamp(contentCount * 50, 150, 350));
        }
    }
}

public record VendorItemsListData(EntProtoId ItemProtoID, string ItemText, int ItemIndex) : ListData;
