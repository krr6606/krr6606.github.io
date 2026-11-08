using Jay;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Xml;

//[Facade Pattern] 데이터 표들이 들어가는 단일 창구
// 데이터 종류별 테이블(Genres, Keywords, Items)을 '보유'하고
// 로딩 순서를 조율하는 역할
// 실제 로드/ 해석/ 보관/ 조회는 각각의 테이블이 책임집니다.

// 새 데이터 종류를 추가하고 싶으면 
// 1. Table클래스를 만들고
// 2. DataManager에 추가하면 되는 구조입니다.
public class DataManager
{

    private static readonly DataManager instance = new DataManager();
    public static DataManager Instance => instance;

    // 읽기(Get 계열)는 동시에 여러 스레드 허용, 쓰기(Load)는 배타적으로 처리
    // ReaderWriterLockSlim : 동기적인 코드에서 쓰는 최신 락,읽기는 병렬 가능, 쓰기는 한번에 하나씩(쓰는동안에는 읽기도 대기) (일반 락보다 무거움)
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
    
    private Dictionary<CategoryType, IReadOnlyList<ItemDef>> itemsByCategory;
    public IReadOnlyList<ItemDef> GetItemDefsByCategory(CategoryType type) => itemsByCategory[type];
    public GenreTable Genres { get; } = new GenreTable();
    public KeyWordTable Keywords { get; } = new KeyWordTable();
    public ItemCategoryTable ItemCategories { get; } = new ItemCategoryTable();
    public ItemTable Items { get; } = new ItemTable();

    public bool IsReady { get; private set; } = false;
    public event Action OnReady;

    private DataManager() { }

    public void Load()
    {

            Genres.Load();
            Keywords.Load();
            ItemCategories.Load();
            Items.Load();
            IsReady = true;
            itemsByCategory = Items.GetListAll()
                .GroupBy(x => x.CategoryType)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<ItemDef>)g.ToArray());
            Console.WriteLine($"[DataManager] : Loaded {Genres.Count} Genres, {Keywords.Count} Keywords, {ItemCategories.Count} ItemCategories, {Items.Count} Items");
            
        OnReady?.Invoke();
    }

    public GenreDef GetGenreDef(int id)
    {
        _lock.EnterReadLock();
        try { return Genres.Get(id); }
        finally { _lock.ExitReadLock(); }
    }

    public KeyWordDef GetKeyWordDef(int id)
    {
        _lock.EnterReadLock();
        try { return Keywords.Get(id); }
        finally { _lock.ExitReadLock(); }
    }

    public ItemCategoryDef GetItemCategoryDef(int id)
    {
        _lock.EnterReadLock();
        try { return ItemCategories.Get(id); }
        finally { _lock.ExitReadLock(); }
    }
    public List<CategoryType> GetAllTypes()
    {
        return ItemCategories.GetAllTypes();
    }
    public ItemDef GetItemDef(int id)
    {
        _lock.EnterReadLock();
        try { return Items.Get(id); }
        finally { _lock.ExitReadLock(); }
    }

    public List<ItemDef> GetAllItemDefs()
    {
        _lock.EnterReadLock();
        try { return Items.GetListAll(); }
        finally { _lock.ExitReadLock(); }
    }



    public List<KeyWordDef> GetKeyWordDefsByGenre(string genre)
    {
        _lock.EnterReadLock();
        try
        {
            List<KeyWordDef> keyWordDefs = new List<KeyWordDef>();
            foreach (var keyWordDef in Keywords.GetAllList())
            {
                if (keyWordDef.GenreName == genre)
                {
                    keyWordDefs.Add(keyWordDef);
                }
            }
            return keyWordDefs;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}