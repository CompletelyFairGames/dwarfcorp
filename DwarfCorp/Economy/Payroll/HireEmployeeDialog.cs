using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DwarfCorp.Gui;
using DwarfCorp.Gui.Widgets;
using Microsoft.Xna.Framework;

namespace DwarfCorp.Gui.Widgets
{
    public class HireEmployeeDialog : Widget
    {
        public Faction Faction;
        public CompanyInformation Company;
        private Button HireButton;

        public Applicant GenerateApplicant(CompanyInformation info, String type)
        {
            Applicant applicant = new Applicant();
            applicant.GenerateRandom(type, 0, info);
            return applicant;
        }

        public HireEmployeeDialog(CompanyInformation _Company)
        {
            Company = _Company;
        }

        public override void Construct()
        {
            Border = "border-fancy";

            int w = Math.Min(Math.Max(2*(Root.RenderData.VirtualScreen.Width/3), 400), 600);
            int h = Math.Min(Math.Max(2*(Root.RenderData.VirtualScreen.Height/3), 600), 700);
            Rect = new Rectangle(Root.RenderData.VirtualScreen.Center.X - w / 2, Root.RenderData.VirtualScreen.Center.Y - h/2, w, h);

            var playerClasses = Library.EnumerateClasses().Where(c => c.PlayerClass).ToList();

            var left = AddChild(new Widget()
            {
                AutoLayout = AutoLayout.DockTop,
                Padding = new Margin(5, 5, 32, 32),
                MinimumSize = new Point(32 * 2 * playerClasses.Count, 48 * 2 + 40)
            });

            var right = AddChild(new Widget()
            {
                AutoLayout = AutoLayout.DockFill
            });


            var buttonRow = right.AddChild(new Widget
            {
                AutoLayout = AutoLayout.DockBottom,
                MinimumSize = new Point(0, 30)
            });

            var applicantInfo = right.AddChild(new ApplicantInfo
            {
                AutoLayout = AutoLayout.DockFill
            }) as ApplicantInfo;


            applicantInfo.Hidden = true;
            left.AddChild(new Widget
            {
                Text = "Applicants",
                AutoLayout = AutoLayout.DockTop,
                MinimumSize = new Point(0, 20),
                Font = "font16"
            });


            foreach (var job in playerClasses)
            {
                var newJob = job.Name;
                var frame = left.AddChild(new Widget()
                {
                    MinimumSize = new Point(32*2, 48*2 + 15),
                    AutoLayout = AutoLayout.DockLeft
                });

                var idx = EmployeePanel.GetIconIndex(job.Name);

                frame.AddChild(new ImageButton()
                {
                    Tooltip = "Click to review applications for " + job.Name,
                    AutoLayout = AutoLayout.DockTop,
                    TextHorizontalAlign = HorizontalAlign.Center,
                    TextVerticalAlign = VerticalAlign.Bottom,
                    OnClick = (sender, args) =>
                    {
                        applicantInfo.Hidden = false;
                        HireButton.Hidden = false;
                        HireButton.Invalidate();
                        applicantInfo.Applicant = GenerateApplicant(Company, newJob);
                    },
                    Background = idx >= 0 ? new TileReference("dwarves", idx) : null,
                    MinimumSize = new Point(32 * 2, 48 * 2),
                    MaximumSize = new Point(32 * 2, 48 * 2)
                });

                frame.AddChild(new Widget()
                {
                    Text = job.Name,
                    MinimumSize = new Point(0, 15),
                    TextColor = Color.Black.ToVector4(),
                    Font = "font8",
                    AutoLayout = AutoLayout.DockTop,
                    TextHorizontalAlign = HorizontalAlign.Center
                });
            }

            buttonRow.AddChild(new Widget
            {
                Text = "Back",
                Border = "border-button",
                AutoLayout = AutoLayout.DockLeft,
                OnClick = (sender, args) =>
                {
                    this.Close();
                }
            });

            HireButton = buttonRow.AddChild(new Button
            {
                Text = "Hire",
                Border = "border-button",
                AutoLayout = AutoLayout.DockRight,
                OnClick = (sender, args) =>
                {
                    var applicant = applicantInfo.Applicant;
                    if (applicant != null)
                    {
#if DEMO
                        if (applicant.Class.Name == "Wizard")
                        {
                            Root.ShowModalPopup(new Gui.Widgets.Confirm() { CancelText = "", Text = "Magic not available in demo." });
                            return;
                        }
#endif
                        if (GameSettings.Default.SigningBonus > Faction.Economy.Funds)
                        {
                            Root.ShowModalPopup(Root.ConstructWidget(new Gui.Widgets.Popup
                            {
                                Text = "We can't afford the signing bonus!",
                            }));
                        }
                        else if (!Faction.GetRooms().Any(r => r.RoomData.Name == "Balloon Port"))
                        {
                            Root.ShowModalPopup(Root.ConstructWidget(new Gui.Widgets.Popup
                            {
                                Text = "We need a balloon port to hire someone.",
                            }));
                        }
                        else if (Faction.Minions.Count + Faction.NewArrivals.Count >= GameSettings.Default.MaxDwarfs)
                        {
                            Root.ShowModalPopup(Root.ConstructWidget(new Gui.Widgets.Popup
                            {
                                Text = String.Format("Can't hire any more dwarfs. We can only have {0}.", GameSettings.Default.MaxDwarfs)
                            }));
                        }
                        else
                        {
                            var date = Faction.Hire(applicant, 1);
                            SoundManager.PlaySound(ContentPaths.Audio.cash, 0.5f);
                            applicantInfo.Hidden = true;
                            HireButton.Hidden = true;
                            Root.ShowModalPopup(new Gui.Widgets.Popup()
                            {
                                Text = String.Format("We hired {0}, paying a signing bonus of {1}. They will arrive in about {2} hour(s).",
                                applicant.Name,
                                GameSettings.Default.SigningBonus,
                                (date - Faction.World.Time.CurrentDate).Hours),
                            });
 
                        }
                    }
                },
                Hidden = true
            }) as Button;
            this.Layout();
        }
    }
}
