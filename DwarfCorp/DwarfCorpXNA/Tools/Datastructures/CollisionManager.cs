// CollisionManager.cs
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
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

namespace DwarfCorp
{
    [JsonObject(IsReference = true)]
    public class CollisionManager
    {
        [Flags]
        public enum CollisionType
        {
            None = 0,
            Static = 2,
            Dynamic = 4,
            Both = Static | Dynamic
        }

        public OctTreeNode<Body> Tree;

        public CollisionManager()
        {
            
        }

        public CollisionManager(BoundingBox bounds)
        {
            Tree = new OctTreeNode<Body>(bounds.Min, bounds.Max);
        }

        public void AddObject(Body bounded)
        {
            Tree.AddItem(bounded, bounded.GetBoundingBox());
        }

        public void RemoveObject(Body bounded, BoundingBox oldLocation)
        {
            Tree.RemoveItem(bounded, oldLocation);
        }

        public IEnumerable<Body> EnumerateIntersectingObjects(BoundingBox box, CollisionType queryType)
        {
            PerformanceMonitor.PushFrame("CollisionManager.EnumerateIntersectingObjects");
            var hash = new HashSet<Body>();
            Tree.EnumerateItems(box, hash, t => (t.CollisionType & queryType) == t.CollisionType);
            PerformanceMonitor.PopFrame();
            return hash;
        }

        public IEnumerable<Body> EnumerateIntersectingObjects(BoundingFrustum Frustum)
        {
            PerformanceMonitor.PushFrame("CollisionManager.EnumerateFrustum");
            var hash = new HashSet<Body>();
            Tree.EnumerateItems(Frustum, hash);
            PerformanceMonitor.PopFrame();
            return hash;
        }

        public IEnumerable<Body> EnumerateAll()
        {
            var hash = new HashSet<Body>();
            Tree.EnumerateAll(hash);
            return hash;
        }

        public void EnumerateBounds(Action<BoundingBox, int> Callback)
        {
            Tree.EnumerateBounds(0, Callback);
        }
    }
}