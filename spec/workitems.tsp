namespace ApiPlatform;

/** Lifecycle status of a work item. */
enum WorkItemStatus {
  OPEN,
  IN_PROGRESS,
  DONE,
}

/**
 * A tracked work item.
 * Stub model for external project-management integration (future domain).
 */
model WorkItem {
  /** Opaque identifier (UUID). */
  @format("uuid")
  id: string;

  /** Short human-readable title. */
  title: string;

  status: WorkItemStatus;

  /**
   * Email address of the assignee.
   * Optional; absent when unassigned.
   */
  @format("email")
  assignee?: string;
}
