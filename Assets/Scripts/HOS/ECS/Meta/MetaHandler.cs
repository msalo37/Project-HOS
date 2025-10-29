using System;
using System.Collections.Generic;
using HOS.ECS.Entity;
using UnityEngine;

namespace HOS.ECS.Meta
{
    public interface IMetaData { }

    public struct MetaKey
    {
        public MetaKey(long key)
        {
            this.key = key;
        }

        public long key;
    }
    
    [System.Serializable]
    public class MetaHandler : ISerializationCallbackReceiver, IEntityAdditionalResourceProvider
    {
        private Dictionary<Guid, Dictionary<MetaKey, IMetaData>> map = new();

        [SerializeField] private List<SerializedMeta> serializedMetaList;

        private Dictionary<MetaKey, IMetaData> GetOrCreate(Guid guid)
        {
            if(map.TryGetValue(guid, out var meta))
                return meta;

            meta = new Dictionary<MetaKey, IMetaData>();
            map[guid] = meta;
            return meta;
        }

        private bool TryGetMetaData(Guid guid, MetaKey key, out IMetaData metaData)
        {
            metaData = default;
            return map.TryGetValue(guid, out var meta) && meta.TryGetValue(key, out metaData);
        }

        public static MetaKey GenerateNewKey()
        {
            Span<byte> spanBytes = stackalloc byte[8];

            System.Security.Cryptography.RandomNumberGenerator.Fill(spanBytes);

            var key = new MetaKey(BitConverter.ToInt64(spanBytes));

            return key;
        }

        public void DeleteAllMeta(Guid id)
        {
            if(map.TryGetValue(id, out var meta))
            {
                if (meta.Count > 0)
                {
                    foreach (var metaData in meta.Values)
                    {
                        if (metaData is IDisposable toDispose)
                        {
                            toDispose.Dispose();
                        }
                    }
                }

                map.Remove(id);
            }
        }

        public void GetAllMetaNoAlloc(Guid guid, Dictionary<MetaKey, IMetaData> noAllocDict)
        {
            if (map.TryGetValue(guid, out var meta))
            {
                foreach (var kvp in meta)
                {
                    noAllocDict.Add(kvp.Key, kvp.Value);
                }
            }
        }

        public void DeleteMeta(BaseEntity entity, MetaKey key)
        {
            if (map.TryGetValue(entity.GUID, out var meta) && meta.TryGetValue(key, out var metaData))
            {
                if (metaData is IDisposable toDispose)
                {
                    toDispose.Dispose();
                }
                meta.Remove(key);
            }
        }
        
        public bool TryGetMeta(Guid guid, MetaKey key, out IMetaData metaData)
        {
            return TryGetMetaData(guid, key, out metaData);
        }

        public bool TryGetMeta<TMeta>(Guid guid, MetaKey key, out TMeta metaData) where TMeta : class, IMetaData
        {
            if(TryGetMetaData(guid, key, out var metaDataRaw) && metaDataRaw is TMeta tMeta)
            {
                metaData = tMeta;
                return true;
            }
            else
            {
                metaData = default(TMeta);
                return false;
            }
        }
       
        public IMetaData GetMeta(Guid guid, MetaKey key)
        {
            if (TryGetMetaData(guid, key, out var meta))
                return meta;
            else
                return default;
        }

        public TMeta GetMeta<TMeta>(Guid guid, MetaKey key) where TMeta : class, IMetaData
        {
            var metaData = GetMeta(guid, key);
            return (TMeta)metaData;
        }

        public MetaKey AddMeta(Guid guid, IMetaData metaData)
        {
            var meta = GetOrCreate(guid);
            var key = GenerateNewKey();
            meta[key] = metaData;

            return key;
        }

        public MetaKey AddMeta<TMeta>(Guid guid, TMeta metaData) where TMeta : class, IMetaData
        {
            return AddMeta(guid, (IMetaData)metaData);
        }

        public void SetMeta(Guid guid, MetaKey key, IMetaData metaData)
        {
            var meta = GetOrCreate(guid);
            meta[key] = metaData;
        }

        public void SetMeta<TMeta>(Guid guid, MetaKey key, TMeta metaData) where TMeta : class, IMetaData
        {
            SetMeta(guid, key, (IMetaData)metaData);
        }

        void IEntityAdditionalResourceProvider.OnAddEntity(BaseEntity entity)
        {
            
        }

        void IEntityAdditionalResourceProvider.OnRemoveEntity(BaseEntity entity)
        {
            DeleteAllMeta(entity.GUID);
        }

        void IEntityAdditionalResourceProvider.Clear()
        {
            map.Clear();
        }

        #region serialization
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            serializedMetaList ??= new List<SerializedMeta>();
            serializedMetaList.Clear();

            foreach (var pair in map)
            {
                var meta = new SerializedMeta(pair.Key, pair.Value);
                serializedMetaList.Add(meta);
            }            
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            map.Clear();
            foreach(var data in serializedMetaList)
            {
                data.DeserizlieIn(map);
            }
        }
        #endregion
    }

    internal interface IEntityAdditionalResourceProvider
    {
        void OnAddEntity(BaseEntity entity);
        void OnRemoveEntity(BaseEntity entity);
        void Clear();
    }

    [Serializable]
    public class SerializedKeyValuePair<K,V>
    {
        public SerializedKeyValuePair(K key, V value)
        {
            this.key = key;
            this.value = value;
        }

        public K key;
        public V value;
    }

    [System.Serializable]
    public class SerializedMeta
    {
        public string guid;
        public List<SerializedKeyValuePair<MetaKey, SerializedMetaData>> meta;

        public SerializedMeta(Guid guid, Dictionary<MetaKey, IMetaData> metaData)
        {
            this.guid = guid.ToString("N");
            meta = new(metaData.Count);

            foreach(var pair in metaData)
            {
                meta.Add(new SerializedKeyValuePair<MetaKey, SerializedMetaData>(pair.Key, new SerializedMetaData(pair.Value)));
            }
        }

        public void DeserizlieIn(Dictionary<Guid, Dictionary<MetaKey, IMetaData>> map)
        {
            Guid g = Guid.Parse(guid);
            var mt = new Dictionary<MetaKey, IMetaData>(meta.Count);

            foreach(var pair in meta)
            {
                mt.Add(pair.key, pair.value.Deserialize());
            }

            map.Add(g, mt);
        }
    }

    [Serializable]
    public class SerializedMetaData
    {
        public string type;
        public string json;

        public SerializedMetaData(IMetaData metaData)
        {
            type = metaData.GetType().FullName;
            json = JsonUtility.ToJson(metaData);
        }

        public IMetaData Deserialize()
        {
            var t = Type.GetType(type);
            return (IMetaData)JsonUtility.FromJson(json, t);
        }
    }
}