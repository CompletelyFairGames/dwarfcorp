// MolemanMinerClass.cs
// 
//  Modified MIT License (MIT)
//  
//  Copyright (c) 2015 Completely Fair Games Ltd.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// The following content pieces are considered PROPRIETARY and may not be used
// in any derivative works, commercial or non commercial, without explicit 
// written permission from Completely Fair Games:
// 
// * Images (sprites, textures, etc.)
// * 3D Models
// * Sound Effects
// * Music
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace DwarfCorp
{
    public class MolemanClass : EmployeeClass
    {
        void InitializeLevels()
        {
            Levels = new List<Level>
            {
                new Level
                {
                    Index = 0,
                    Name = "Mining Intern",
                    Pay = 25,
                    XP = 0,
                    BaseStats = new StatAdjustment(5)
                },
                new Level
                {
                    Index = 1,
                    Name = "Assistant Miner",
                    Pay = 50,
                    XP = 100,
                    BaseStats = new StatAdjustment(5)
                    {
                        Strength = 6,
                        Constitution = 6,
                        Charisma = 6
                    }
                },
                new Level
                {
                    Index = 2,
                    Name = "Miner",
                    Pay = 100,
                    XP = 250,
                    BaseStats = new StatAdjustment(5)
                    {
                        Strength = 7,
                        Constitution = 6,
                        Charisma = 6
                    }
                },
                new Level
                {
                    Index = 3,
                    Name = "Mine Specialist",
                    Pay = 200,
                    XP = 500,
                    BaseStats = new StatAdjustment(5)
                    {
                        Strength = 7,
                        Constitution = 7,
                        Charisma = 6,
                        Dexterity = 6
                    }
                },
                new Level
                {
                    Index = 4,
                    Name = "Senior Mine Specialist",
                    Pay = 500,
                    XP = 1000,
                    BaseStats = new StatAdjustment(5)
                    {
                        Strength = 8,
                        Constitution = 7,
                        Charisma = 6,
                        Dexterity = 6
                    }
                },
                new Level
                {
                    Index = 5,
                    Name = "Principal Mine Specialist",
                    Pay = 1000,
                    XP = 5000,
                    BaseStats = new StatAdjustment(5)
                    {
                        Strength = 9,
                        Constitution = 8,
                        Charisma = 7,
                        Dexterity = 7
                    }
                },
                new Level
                {
                    Index = 6,
                    Name = "Vice President of Mine Operations",
                    Pay = 5000,
                    XP = 10000,
                    BaseStats = new StatAdjustment(5)
                    {
                        Strength = 10,
                        Constitution = 8,
                        Charisma = 8,
                        Dexterity = 8
                    }
                },
                new Level
                {
                    Index = 7,
                    Name = "President of Mine Operations",
                    Pay = 10000,
                    XP = 20000,
                    BaseStats = new StatAdjustment(5)
                    {
                        Strength = 10,
                        Constitution = 9,
                        Charisma = 9,
                        Dexterity = 9,
                        Intelligence = 6
                    }

                },
                new Level
                {
                    Index = 8,
                    Name = "Ascended Mine Master",
                    Pay = 50000,
                    XP = 1000000,
                    BaseStats = new StatAdjustment(5)
                    {
                        Strength = 10,
                        Constitution = 10,
                        Charisma = 10,
                        Dexterity = 10,
                        Intelligence = 6
                    }
                },
                new Level
                {
                    Index = 9,
                    Name = "High Mine Lord",
                    Pay = 100000,
                    XP = 2000000,
                    BaseStats = new StatAdjustment(5)
                    {
                        Strength = 10,
                        Constitution = 10,
                        Charisma = 10,
                        Dexterity = 10,
                        Intelligence = 10
                    }
                },
                new Level
                {
                    Index = 10,
                    Name = "Father of All Miners",
                    Pay = 100000,
                    XP = 5000000,
                    BaseStats = new StatAdjustment(5)
                    {
                        Strength = 10,
                        Constitution = 10,
                        Charisma = 10,
                        Dexterity = 10,
                        Intelligence = 10
                    }
                }
            };
        }

        void InitializeActions()
        {
            Actions =
                Task.TaskCategory.Chop |
                Task.TaskCategory.Dig |
                Task.TaskCategory.Attack |
                Task.TaskCategory.Gather |
                Task.TaskCategory.TillSoil |
                Task.TaskCategory.Plant |
                Task.TaskCategory.Wrangle;
        }

        public void InitializeWeapons()
        {
            Attacks = new List<Attack>()
            {
                new Attack("Claws", 1.5f, 0.5f, 2.0f, SoundSource.Create(ContentPaths.Audio.Oscar.sfx_ic_moleman_claw_attack_1, ContentPaths.Audio.Oscar.sfx_ic_moleman_claw_attack_2, ContentPaths.Audio.Oscar.sfx_ic_moleman_claw_attack_3), ContentPaths.Effects.claw)
                {
                    Knockback = 2.5f,
                    TriggerMode = Attack.AttackTrigger.Animation,
                    TriggerFrame = 2
                }
            };
        }

        protected override sealed void InitializeStatics()
        {
            Name = "Moleman Miner";
            InitializeLevels();
            InitializeWeapons();
            InitializeActions();
            base.InitializeStatics();
        }

        public MolemanClass()
        {

        }

        public MolemanClass(bool initialize)
        {
            if (initialize && !staticsInitiailized)
            {
                InitializeStatics();
            }
        }
    }
}
