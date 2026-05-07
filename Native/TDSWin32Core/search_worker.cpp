#include "search_worker.hpp"

#include "file_index.hpp"
#include "tds_keyindex.hpp"

#include <cwctype>
#include <shlwapi.h>
#include <vector>

#pragma comment(lib, "shlwapi.lib")

static std::wstring trim_copy(std::wstring const& s) {
  std::size_t a = 0;
  while (a < s.size() && iswspace(s[a]))
    ++a;
  std::size_t b = s.size();
  while (b > a && iswspace(s[b - 1]))
    --b;
  return s.substr(a, b - a);
}

static void split_words(std::wstring const& s, std::vector<std::wstring>& words) {
  std::wstring cur;
  for (wchar_t c : s) {
    if (iswspace(c)) {
      if (!cur.empty()) {
        words.push_back(trim_copy(cur));
        cur.clear();
      }
    } else
      cur.push_back(c);
  }
  if (!cur.empty())
    words.push_back(trim_copy(cur));
}

static std::wstring flatten_upper(std::vector<std::wstring> const& words) {
  std::wstring out;
  for (auto const& w : words) {
    for (wchar_t c : w) {
      out.push_back(static_cast<wchar_t>(std::towupper(static_cast<wint_t>(c))));
    }
  }
  return out;
}

static bool all_words_in_name(std::wstring const& name, std::vector<std::wstring> const& words) {
  for (auto const& w : words) {
    if (w.empty())
      continue;
    if (StrStrIW(name.c_str(), w.c_str()) == nullptr)
      return false;
  }
  return true;
}

SearchWorker::SearchWorker() = default;

SearchWorker::~SearchWorker() {
  cancel();
  std::lock_guard lk(mutex_);
  if (worker_.joinable())
    worker_.join();
}

void SearchWorker::cancel() {
  cancel_.store(true, std::memory_order_release);
}

void SearchWorker::start_search(FileIndex const* index, std::wstring query, int max_hits, DoneCallback on_done) {
  if (!index || max_hits <= 0) {
    if (on_done)
      on_done({});
    return;
  }

  std::wstring q = trim_copy(query);
  if (q.empty()) {
    if (on_done)
      on_done({});
    return;
  }

  std::lock_guard lk(mutex_);
  if (worker_.joinable())
    worker_.join();

  cancel_.store(false, std::memory_order_release);

  worker_ = std::thread([this, index, q = std::move(q), max_hits, cb = std::move(on_done)]() mutable {
    std::vector<std::wstring> words;
    split_words(q, words);
    if (words.empty()) {
      if (cb)
        cb({});
      return;
    }

    std::wstring const flat = flatten_upper(words);
    std::uint64_t const uniwords = tds::keyindex::query_mask_from_flat_keyword(flat);

    std::vector<std::size_t> hits;
    hits.reserve(static_cast<std::size_t>(max_hits));

    index->for_each_record_readlocked([&](FileRecord const& r, std::size_t idx) -> bool {
      if (cancel_.load(std::memory_order_acquire))
        return false;
      if (static_cast<int>(hits.size()) >= max_hits)
        return false;
      if ((uniwords | r.keyindex) != r.keyindex)
        return true;
      if (!all_words_in_name(r.inner_file_name, words))
        return true;
      hits.push_back(idx);
      return static_cast<int>(hits.size()) < max_hits;
    });

    if (cb)
      cb(std::move(hits));
  });
}
