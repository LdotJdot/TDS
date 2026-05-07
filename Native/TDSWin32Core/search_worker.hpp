#pragma once

#include <atomic>
#include <functional>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

class FileIndex;

/// 后台子串搜索（简化：仅文件名；可取消）。与 C# 搜索循环语义接近的占位实现。
class SearchWorker {
public:
  using DoneCallback = std::function<void(std::vector<std::size_t> hits)>;

  SearchWorker();
  ~SearchWorker();

  SearchWorker(SearchWorker const&) = delete;
  SearchWorker& operator=(SearchWorker const&) = delete;

  void cancel();
  /// 若已有任务在跑，会先 join 再启动新任务。
  void start_search(FileIndex const* index, std::wstring query, int max_hits, DoneCallback on_done);

private:
  std::mutex mutex_;
  std::thread worker_;
  std::atomic_bool cancel_{false};
};
