using ScholarTrend.Application.DTOs.Authors;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Mappings;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Application.DTOs.Common;

namespace ScholarTrend.Application.Services;

public class AuthorService : IAuthorService
{
    private readonly IUnitOfWork _unitOfWork;

    public AuthorService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<AuthorListItemDto>> GetPagedAsync(string? keyword, int page, int pageSize)
    {
        var (authors, totalCount) = await _unitOfWork.Authors.GetPagedAsync(keyword, page, pageSize);
        
        var authorIds = authors.Select(a => a.Id).ToList();
        var paperCounts = await _unitOfWork.ResearchPapers.CountByAuthorIdsAsync(authorIds);

        var items = new List<AuthorListItemDto>();
        foreach (var author in authors)
        {
            var paperCount = paperCounts.GetValueOrDefault(author.Id, 0);
            items.Add(MapListItem(author, paperCount));
        }

        return new PagedResult<AuthorListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AuthorDetailDto> GetByIdAsync(int id)
    {
        var author = await _unitOfWork.Authors.GetByIdAsync(id);
        if (author == null)
        {
            throw new InvalidOperationException("Author not found.");
        }

        return await BuildDetailAsync(author);
    }

    public async Task<AuthorDetailDto> GetByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Author name is required.");
        }

        var author = await _unitOfWork.Authors.GetByNameAsync(name);
        if (author == null)
        {
            throw new InvalidOperationException("Author not found.");
        }

        return await BuildDetailAsync(author);
    }

    private async Task<AuthorDetailDto> BuildDetailAsync(Author author)
    {
        var paperCount = await _unitOfWork.ResearchPapers.CountByAuthorAsync(author.Id);
        var recentPapers = await _unitOfWork.ResearchPapers.GetPapersByAuthorAsync(author.Id, limit: 5);

        return new AuthorDetailDto
        {
            Id = author.Id,
            Name = author.Name,
            ExternalId = author.ExternalId,
            Affiliation = author.Affiliation,
            Country = author.Country,
            HIndex = author.HIndex,
            TotalCitations = author.TotalCitations,
            PaperCount = paperCount,
            RecentPapers = recentPapers.Select(PaperMapper.ToListItem).ToList()
        };
    }

    private static AuthorListItemDto MapListItem(Author author, int paperCount)
    {
        return new AuthorListItemDto
        {
            Id = author.Id,
            Name = author.Name,
            Affiliation = author.Affiliation,
            Country = author.Country,
            PaperCount = paperCount
        };
    }
}
