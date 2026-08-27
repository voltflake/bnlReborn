using System.Numerics;
using Octree;

namespace BNLReloadedServer.Octree_Extensions;

#nullable disable
public static class BoundingBoxExtensions
{
    public static bool Intersects(this BoundingBox box, IBoundingShape shape) => shape.Intersects(box);
}

public class BoundsOctreeEx<T>
{
    private Node _rootNode;
    private readonly float _looseness;
    private readonly float _initialSize;
    private readonly float _minSize;

    public int Count { get; private set; }

    public BoundingBox MaxBounds => _rootNode.Bounds;

    public BoundingBox[] GetChildBounds()
    {
        var bounds = new List<BoundingBox>();
        _rootNode.GetChildBounds(bounds);
        return bounds.ToArray();
    }

    public BoundsOctreeEx(
      float initialWorldSize,
      Vector3 initialWorldPos,
      float minNodeSize,
      float loosenessVal)
    {
        if (minNodeSize > (double)initialWorldSize)
            throw new ArgumentException("Minimum node size must be at least as big as the initial world size.", nameof(minNodeSize));
        Count = 0;
        _initialSize = initialWorldSize;
        _minSize = minNodeSize;
        _looseness = MathExtensions.Clamp(loosenessVal, 1f, 2f);
        _rootNode = new Node(_initialSize, _minSize, _looseness, initialWorldPos);
    }

    public void Add(T obj, BoundingBox objBounds)
    {
        var num = 0;
        while (!_rootNode.Add(obj, objBounds))
        {
            Grow(objBounds.Center - _rootNode.Center);
            if (++num > 20)
                throw new InvalidOperationException("Aborted Add operation as it seemed to be going on forever " + $"({num - 1} attempts at growing the octree).");
        }
        ++Count;
    }

    public bool Remove(T obj)
    {
        var num = _rootNode.Remove(obj) ? 1 : 0;
        if (num == 0)
            return false;
        --Count;
        Shrink();
        return true;
    }

    public bool Remove(T obj, BoundingBox objBounds)
    {
        var num = _rootNode.Remove(obj, objBounds) ? 1 : 0;
        if (num == 0)
            return false;
        --Count;
        Shrink();
        return true;
    }

    public bool IsColliding(BoundingBox checkBounds) => _rootNode.IsColliding(ref checkBounds);

    public bool IsColliding(IBoundingShape checkBounds) => _rootNode.IsColliding(ref checkBounds);

    public bool IsColliding(Ray checkRay, float maxDistance)
    {
        return _rootNode.IsColliding(ref checkRay, maxDistance);
    }

    public T[] GetColliding(BoundingBox checkBounds)
    {
        var result = new List<T>();
        _rootNode.GetColliding(ref checkBounds, result);
        return result.ToArray();
    }

    public T[] GetColliding(IBoundingShape checkBounds)
    {
        var result = new List<T>();
        _rootNode.GetColliding(ref checkBounds, result);
        return result.ToArray();
    }

    public T[] GetColliding(Ray checkRay, float maxDistance = float.PositiveInfinity)
    {
        var result = new List<T>();
        _rootNode.GetColliding(ref checkRay, result, maxDistance);
        return result.ToArray();
    }

    public bool GetCollidingNonAlloc(List<T> collidingWith, BoundingBox checkBounds)
    {
        collidingWith.Clear();
        _rootNode.GetColliding(ref checkBounds, collidingWith);
        return collidingWith.Count > 0;
    }

    public bool GetCollidingNonAlloc(List<T> collidingWith, IBoundingShape checkBounds)
    {
        collidingWith.Clear();
        _rootNode.GetColliding(ref checkBounds, collidingWith);
        return collidingWith.Count > 0;
    }

    public bool GetCollidingNonAlloc(List<T> collidingWith, Ray checkRay, float maxDistance = float.PositiveInfinity)
    {
        collidingWith.Clear();
        _rootNode.GetColliding(ref checkRay, collidingWith, maxDistance);
        return collidingWith.Count > 0;
    }

    private void Grow(Vector3 direction)
    {
        var num1 = direction.X >= 0.0 ? 1 : -1;
        var num2 = direction.Y >= 0.0 ? 1 : -1;
        var num3 = direction.Z >= 0.0 ? 1 : -1;
        var rootNode = _rootNode;
        var num4 = _rootNode.BaseLength / 2f;
        var baseLengthVal = _rootNode.BaseLength * 2f;
        var centerVal = _rootNode.Center + new Vector3(num1 * num4, num2 * num4, num3 * num4);
        _rootNode = new Node(baseLengthVal, _minSize, _looseness, centerVal);
        if (!rootNode.HasAnyObjects())
            return;
        var num5 = _rootNode.BestFitChild(rootNode.Center);
        var childOctrees = new Node[8];
        for (var index = 0; index < 8; ++index)
        {
            if (index == num5)
            {
                childOctrees[index] = rootNode;
            }
            else
            {
                var num6 = index % 2 == 0 ? -1 : 1;
                var num7 = index > 3 ? -1 : 1;
                var num8 = index is < 2 or > 3 and < 6 ? -1 : 1;
                childOctrees[index] = new Node(rootNode.BaseLength, _minSize, _looseness, centerVal + new Vector3(num6 * num4, num7 * num4, num8 * num4));
            }
        }
        _rootNode.SetChildren(childOctrees);
    }

    private void Shrink() => _rootNode = _rootNode.ShrinkIfPossible(_initialSize);

    private class Node
    {
        private float _looseness;
        private float _minSize;
        private float _adjLength;
        private BoundingBox _bounds;
        private readonly List<OctreeObject> _objects = [];
        private Node[] _children;
        private BoundingBox[] _childBounds;
        private const int NumObjectsAllowed = 8;

        public Vector3 Center { get; private set; }

        public float BaseLength { get; private set; }

        private bool HasChildren => _children != null;

        public BoundingBox Bounds => _bounds;

        public void GetChildBounds(List<BoundingBox> bounds)
        {
            if (HasChildren)
            {
                foreach (var child in _children)
                    child.GetChildBounds(bounds);
            }
            else
                bounds.Add(Bounds);
        }

        public Node(float baseLengthVal, float minSizeVal, float loosenessVal, Vector3 centerVal)
        {
            SetValues(baseLengthVal, minSizeVal, loosenessVal, centerVal);
        }

        public bool Add(T obj, BoundingBox objBounds)
        {
            if (!Encapsulates(_bounds, objBounds))
                return false;
            SubAdd(obj, objBounds);
            return true;
        }

        public bool Remove(T obj)
        {
            var flag = false;
            for (var index = 0; index < _objects.Count; ++index)
            {
                if (!_objects[index].Obj.Equals(obj)) continue;
                flag = _objects.Remove(_objects[index]);
                break;
            }
            if (!flag && _children != null)
            {
                for (var index = 0; index < 8; ++index)
                {
                    flag = _children[index].Remove(obj);
                    if (flag)
                        break;
                }
            }
            if (flag && _children != null && ShouldMerge())
                Merge();
            return flag;
        }

        public bool Remove(T obj, BoundingBox objBounds)
        {
            return Encapsulates(_bounds, objBounds) && SubRemove(obj, objBounds);
        }

        public bool IsColliding(ref BoundingBox checkBounds)
        {
            if (!_bounds.Intersects(checkBounds))
                return false;
            foreach (var t in _objects)
            {
                if (t.Bounds.Intersects(checkBounds))
                    return true;
            }

            if (_children == null) return false;
            for (var index = 0; index < 8; ++index)
            {
                if (_children[index].IsColliding(ref checkBounds))
                    return true;
            }
            return false;
        }

        public bool IsColliding(ref IBoundingShape checkBounds)
        {
            if (!_bounds.Intersects(checkBounds))
                return false;
            foreach (var t in _objects)
            {
                if (t.Bounds.Intersects(checkBounds))
                    return true;
            }

            if (_children == null) return false;
            for (var index = 0; index < 8; ++index)
            {
                if (_children[index].IsColliding(ref checkBounds))
                    return true;
            }
            return false;
        }

        public bool IsColliding(ref Ray checkRay, float maxDistance = float.PositiveInfinity)
        {
            if (!_bounds.IntersectRay(checkRay, out var distance) || distance > (double)maxDistance)
                return false;
            foreach (var t in _objects)
            {
                if (t.Bounds.IntersectRay(checkRay, out distance) && distance <= (double)maxDistance)
                    return true;
            }

            if (_children == null) return false;
            for (var index = 0; index < 8; ++index)
            {
                if (_children[index].IsColliding(ref checkRay, maxDistance))
                    return true;
            }
            return false;
        }

        public void GetColliding(ref BoundingBox checkBounds, List<T> result)
        {
            if (!_bounds.Intersects(checkBounds))
                return;
            foreach (var t in _objects)
            {
                if (t.Bounds.Intersects(checkBounds))
                    result.Add(t.Obj);
            }
            if (_children == null)
                return;
            for (var index = 0; index < 8; ++index)
                _children[index].GetColliding(ref checkBounds, result);
        }

        public void GetColliding(ref IBoundingShape checkBounds, List<T> result)
        {
            if (!_bounds.Intersects(checkBounds))
                return;
            foreach (var t in _objects)
            {
                if (t.Bounds.Intersects(checkBounds))
                    result.Add(t.Obj);
            }
            if (_children == null)
                return;
            for (var index = 0; index < 8; ++index)
                _children[index].GetColliding(ref checkBounds, result);
        }

        public void GetColliding(ref Ray checkRay, List<T> result, float maxDistance = float.PositiveInfinity)
        {
            if (!_bounds.IntersectRay(checkRay, out var distance) || distance > (double)maxDistance)
                return;
            foreach (var t in _objects)
            {
                if (t.Bounds.IntersectRay(checkRay, out distance) && distance <= (double)maxDistance)
                    result.Add(t.Obj);
            }
            if (_children == null)
                return;
            for (var index = 0; index < 8; ++index)
                _children[index].GetColliding(ref checkRay, result, maxDistance);
        }

        public void SetChildren(Node[] childOctrees)
        {
            _children = childOctrees.Length == 8 ? childOctrees : throw new ArgumentException("Child octree array must be length 8. Was length: " + childOctrees.Length, nameof(childOctrees));
        }

        public Node ShrinkIfPossible(float minLength)
        {
            if (BaseLength < 2.0 * minLength || _objects.Count == 0 && (_children == null || _children.Length == 0))
                return this;
            var index1 = -1;
            for (var index2 = 0; index2 < _objects.Count; ++index2)
            {
                var octreeObject = _objects[index2];
                var index3 = BestFitChild(octreeObject.Bounds.Center);
                if (index2 != 0 && index3 != index1 || !Encapsulates(_childBounds[index3], octreeObject.Bounds))
                    return this;
                if (index1 < 0)
                    index1 = index3;
            }
            if (_children != null)
            {
                var flag = false;
                for (var index4 = 0; index4 < _children.Length; ++index4)
                {
                    if (!_children[index4].HasAnyObjects()) continue;
                    if (flag || index1 >= 0 && index1 != index4)
                        return this;
                    flag = true;
                    index1 = index4;
                }
            }

            if (_children != null) return index1 == -1 ? this : _children[index1];
            SetValues(BaseLength / 2f, _minSize, _looseness, _childBounds[index1].Center);
            return this;
        }

        public int BestFitChild(Vector3 objBoundsCenter)
        {
            return (objBoundsCenter.X <= (double)Center.X ? 0 : 1) + (objBoundsCenter.Y >= (double)Center.Y ? 0 : 4) + (objBoundsCenter.Z <= (double)Center.Z ? 0 : 2);
        }

        public bool HasAnyObjects()
        {
            if (_objects.Count > 0)
                return true;
            if (_children == null) return false;
            for (var index = 0; index < 8; ++index)
            {
                if (_children[index].HasAnyObjects())
                    return true;
            }
            return false;
        }

        private void SetValues(
          float baseLengthVal,
          float minSizeVal,
          float loosenessVal,
          Vector3 centerVal)
        {
            BaseLength = baseLengthVal;
            _minSize = minSizeVal;
            _looseness = loosenessVal;
            Center = centerVal;
            _adjLength = _looseness * baseLengthVal;
            _bounds = new BoundingBox(Center, new Vector3(_adjLength, _adjLength, _adjLength));
            var num1 = BaseLength / 4f;
            var num2 = BaseLength / 2f * _looseness;
            var size = new Vector3(num2, num2, num2);
            _childBounds = new BoundingBox[8];
            _childBounds[0] = new BoundingBox(Center + new Vector3(-num1, num1, -num1), size);
            _childBounds[1] = new BoundingBox(Center + new Vector3(num1, num1, -num1), size);
            _childBounds[2] = new BoundingBox(Center + new Vector3(-num1, num1, num1), size);
            _childBounds[3] = new BoundingBox(Center + new Vector3(num1, num1, num1), size);
            _childBounds[4] = new BoundingBox(Center + new Vector3(-num1, -num1, -num1), size);
            _childBounds[5] = new BoundingBox(Center + new Vector3(num1, -num1, -num1), size);
            _childBounds[6] = new BoundingBox(Center + new Vector3(-num1, -num1, num1), size);
            _childBounds[7] = new BoundingBox(Center + new Vector3(num1, -num1, num1), size);
        }

        private void SubAdd(T obj, BoundingBox objBounds)
        {
            if (!HasChildren)
            {
                if (_objects.Count < 8 || BaseLength / 2.0 < _minSize)
                {
                    _objects.Add(new OctreeObject
                    {
                        Obj = obj,
                        Bounds = objBounds
                    });
                    return;
                }
                if (_children == null)
                {
                    Split();
                    if (_children == null)
                        throw new InvalidOperationException("Child creation failed for an unknown reason. Early exit.");
                    for (var index1 = _objects.Count - 1; index1 >= 0; --index1)
                    {
                        var octreeObject = _objects[index1];
                        var index2 = BestFitChild(octreeObject.Bounds.Center);
                        if (!Encapsulates(_children[index2]._bounds, octreeObject.Bounds)) continue;
                        _children[index2].SubAdd(octreeObject.Obj, octreeObject.Bounds);
                        _objects.Remove(octreeObject);
                    }
                }
            }
            var index = BestFitChild(objBounds.Center);
            if (Encapsulates(_children[index]._bounds, objBounds))
                _children[index].SubAdd(obj, objBounds);
            else
                _objects.Add(new OctreeObject
                {
                    Obj = obj,
                    Bounds = objBounds
                });
        }

        private bool SubRemove(T obj, BoundingBox objBounds)
        {
            var flag = false;
            for (var index = 0; index < _objects.Count; ++index)
            {
                if (!_objects[index].Obj.Equals(obj)) continue;
                flag = _objects.Remove(_objects[index]);
                break;
            }
            if (!flag && _children != null)
                flag = _children[BestFitChild(objBounds.Center)].SubRemove(obj, objBounds);
            if (flag && _children != null && ShouldMerge())
                Merge();
            return flag;
        }

        private void Split()
        {
            var num = BaseLength / 4f;
            var baseLengthVal = BaseLength / 2f;
            _children = new Node[8];
            _children[0] = new Node(baseLengthVal, _minSize, _looseness, Center + new Vector3(-num, num, -num));
            _children[1] = new Node(baseLengthVal, _minSize, _looseness, Center + new Vector3(num, num, -num));
            _children[2] = new Node(baseLengthVal, _minSize, _looseness, Center + new Vector3(-num, num, num));
            _children[3] = new Node(baseLengthVal, _minSize, _looseness, Center + new Vector3(num, num, num));
            _children[4] = new Node(baseLengthVal, _minSize, _looseness, Center + new Vector3(-num, -num, -num));
            _children[5] = new Node(baseLengthVal, _minSize, _looseness, Center + new Vector3(num, -num, -num));
            _children[6] = new Node(baseLengthVal, _minSize, _looseness, Center + new Vector3(-num, -num, num));
            _children[7] = new Node(baseLengthVal, _minSize, _looseness, Center + new Vector3(num, -num, num));
        }

        private void Merge()
        {
            for (var index1 = 0; index1 < 8; ++index1)
            {
                var child = _children[index1];
                for (var index2 = child._objects.Count - 1; index2 >= 0; --index2)
                    _objects.Add(child._objects[index2]);
            }
            _children = null;
        }

        private static bool Encapsulates(BoundingBox outerBounds, BoundingBox innerBounds)
        {
            return outerBounds.Contains(innerBounds.Min) && outerBounds.Contains(innerBounds.Max);
        }

        private bool ShouldMerge()
        {
            var count = _objects.Count;
            if (_children == null) return count <= 8;
            foreach (var child in _children)
            {
                if (child._children != null)
                    return false;
                count += child._objects.Count;
            }
            return count <= 8;
        }

        private class OctreeObject
        {
            public T Obj;
            public BoundingBox Bounds;
        }
    }
}
