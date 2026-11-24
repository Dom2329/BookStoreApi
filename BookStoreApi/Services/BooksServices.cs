
using BookStoreApi.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BookStoreApi.Services
{
    public class BooksService
    {
        private readonly IMongoCollection<Book> _booksCollection;

        public BooksService(
            IOptions<BookStoreDatabaseSettings> settings)
        {
            var client = new MongoClient(
                settings.Value.ConnectionString);

            var database = client.GetDatabase(
                settings.Value.DatabaseName);

            _booksCollection = database.GetCollection<Book>(
                settings.Value.BooksCollectionName);
        }

        
        //--------------------------Gets all books---------------------------    
        public async Task<List<Book>> GetAsync()
        {
            return await _booksCollection.Find(_ => true).ToListAsync();
        }
   
        //-------------------------Gets one book---------------------------------      
        public async Task<Book?> GetAsync(string id)
        {
            return await _booksCollection.Find(x => x.Id == id)
                .FirstOrDefaultAsync();
        }
       
        //-----------------Adds a book------------------------------       
        public async Task CreateAsync(Book book)
        {
            await _booksCollection.InsertOneAsync(book);
        }

        
        //--------------------------Updates a book------------------------------      
        public async Task UpdateAsync(string id, Book book)
        {
            await _booksCollection.ReplaceOneAsync(
                x => x.Id == id, book);
        }
        //---------------------------Deletes a book-------------------------        
        public async Task RemoveAsync(string id)
        {
            await _booksCollection.DeleteOneAsync(x => x.Id == id);
        }

        //----------------------------Uploads multiple books--------------------------------------
        public async Task<List<string>> UploadBooksAsync(List<Book> books)
        {
            await _booksCollection.InsertManyAsync(books);

            return books.Select(x => x.Id!).ToList();
        }

        //----------------------Searches books--------------------------------------
        public async Task<List<Book>> SearchAsync(
            string? category, decimal? minPrice, decimal? maxPrice)
        {
            var filter = Builders<Book>.Filter.Empty;

            if (!string.IsNullOrWhiteSpace(category))
            {
                filter &= Builders<Book>.Filter.Eq(x => x.Category, category);
            }

            if (minPrice.HasValue)
            {
                filter &= Builders<Book>.Filter.Gte(x => x.Price, minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                filter &= Builders<Book>.Filter.Lte(x => x.Price, maxPrice.Value);
            }

            return await _booksCollection.Find(filter).ToListAsync();
        }

        //---------------------Gets most expensive books-------------------------------

        public async Task<List<object>> GetTopAsync(int count)
        {
            var books = await _booksCollection
                .Find(Builders<Book>.Filter.Empty)
                .SortByDescending(x => x.Price)
                .Limit(count)
                .Project(x => new
                {
                    x.Name,
                    x.Price
                })
                .ToListAsync();

            return books.Select(x => (object)x).ToList();
        }

        //----------------------------Changes book price------------------------------
    
        public async Task<bool> ChangePriceAsync(string id, decimal price)
        {
            var update = Builders<Book>.Update
                .Set(x => x.Price, price);

            var result = await _booksCollection.UpdateOneAsync(
                x => x.Id == id, update);

            return result.MatchedCount > 0;
        }





    }
}

