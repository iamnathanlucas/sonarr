import React, { useCallback, useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import FieldSet from 'Components/FieldSet';
import IconButton from 'Components/Link/IconButton';
import PageSectionContent from 'Components/Page/PageSectionContent';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TablePager from 'Components/Table/TablePager';
import TableRow from 'Components/Table/TableRow';
import { icons } from 'Helpers/Props';
import * as importListExclusionActions from 'Store/Actions/Settings/importListExclusions';
import {
  registerPagePopulator,
  unregisterPagePopulator,
} from 'Utilities/pagePopulator';
import translate from 'Utilities/String/translate';
import EditImportListExclusionModalConnector from './EditImportListExclusionModalConnector';
import ImportListExclusion from './ImportListExclusion';

const COLUMNS = [
  {
    name: 'title',
    label: () => translate('Title'),
    isVisible: true,
    isSortable: true,
  },
  {
    name: 'tvdbid',
    label: () => translate('TvdbId'),
    isVisible: true,
    isSortable: true,
  },
  {
    name: 'actions',
    isVisible: true,
    isSortable: false,
  },
];

interface ImportListExclusionsProps {
  useCurrentPage: number;
  totalRecords: number;
}

function createImportListExlucionsSelector() {
  return createSelector(
    (state: AppState) => state.settings.importListExclusions,
    (importListExclusions) => {
      return {
        ...importListExclusions,
      };
    }
  );
}

function ImportListExclusions(props: ImportListExclusionsProps) {
  const { useCurrentPage, totalRecords } = props;

  const dispatch = useDispatch();

  const fetchImportListExclusions = useCallback(() => {
    dispatch(importListExclusionActions.fetchImportListExclusions());
  }, [dispatch]);

  const deleteImportListExclusion = useCallback(
    (payload: { id: number }) => {
      dispatch(importListExclusionActions.deleteImportListExclusion(payload));
    },
    [dispatch]
  );

  const gotoImportListExclusionFirstPage = useCallback(() => {
    dispatch(importListExclusionActions.gotoImportListExclusionFirstPage());
  }, [dispatch]);

  const gotoImportListExclusionPreviousPage = useCallback(() => {
    dispatch(importListExclusionActions.gotoImportListExclusionPreviousPage());
  }, [dispatch]);

  const gotoImportListExclusionNextPage = useCallback(() => {
    dispatch(importListExclusionActions.gotoImportListExclusionNextPage());
  }, [dispatch]);

  const gotoImportListExclusionLastPage = useCallback(() => {
    dispatch(importListExclusionActions.gotoImportListExclusionLastPage());
  }, [dispatch]);

  const gotoImportListExclusionPage = useCallback(
    (page: number) => {
      dispatch(
        importListExclusionActions.gotoImportListExclusionPage({ page })
      );
    },
    [dispatch]
  );

  const setImportListExclusionSort = useCallback(
    (sortKey: { sortKey: string }) => {
      dispatch(
        importListExclusionActions.setImportListExclusionSort({ sortKey })
      );
    },
    [dispatch]
  );

  const setImportListTableOption = useCallback(
    (payload: { pageSize: number }) => {
      dispatch(
        importListExclusionActions.setImportListExclusionTableOption(payload)
      );
    },
    [dispatch]
  );

  const repopulate = useCallback(() => {
    gotoImportListExclusionFirstPage();
  }, [gotoImportListExclusionFirstPage]);

  useEffect(() => {
    registerPagePopulator(repopulate);

    if (useCurrentPage) {
      fetchImportListExclusions();
    } else {
      gotoImportListExclusionFirstPage();
    }

    return () => unregisterPagePopulator(repopulate);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const onConfirmDeleteImportListExclusion = useCallback(
    (id: number) => {
      deleteImportListExclusion({ id });
      repopulate();
    },
    [deleteImportListExclusion, repopulate]
  );

  const selected = useSelector(createImportListExlucionsSelector());

  const {
    isFetching,
    isPopulated,
    items,
    pageSize,
    sortKey,
    error,
    sortDirection,
    ...otherProps
  } = selected;

  const [
    isAddImportListExclusionModalOpen,
    setAddImportListExclusionModalOpen,
  ] = useState(false);

  const onModalClose = useCallback(() => {
    setAddImportListExclusionModalOpen(false);
  }, [setAddImportListExclusionModalOpen]);

  const onAddImportListExclusionPress = useCallback(() => {
    setAddImportListExclusionModalOpen(true);
  }, [setAddImportListExclusionModalOpen]);

  const isFetchingForFirstTime = isFetching && !isPopulated;

  return (
    <FieldSet legend={translate('ImportListExclusions')}>
      <PageSectionContent
        errorMessage={translate('ImportListExclusionsLoadError')}
        isFetching={isFetchingForFirstTime}
        isPopulated={isPopulated}
        error={error}
      >
        <Table
          columns={COLUMNS}
          canModifyColumns={false}
          pageSize={pageSize}
          sortKey={sortKey}
          sortDirection={sortDirection}
          onSortPress={setImportListExclusionSort}
          onTableOptionChange={setImportListTableOption}
        >
          <TableBody>
            {items.map((item, index) => {
              return (
                <ImportListExclusion
                  key={item.id}
                  {...item}
                  {...otherProps}
                  index={index}
                  onConfirmDeleteImportListExclusion={
                    onConfirmDeleteImportListExclusion
                  }
                />
              );
            })}

            <TableRow>
              <TableRowCell />
              <TableRowCell />

              <TableRowCell>
                <IconButton
                  name={icons.ADD}
                  onPress={onAddImportListExclusionPress}
                />
              </TableRowCell>
            </TableRow>
          </TableBody>
        </Table>

        <TablePager
          totalRecords={totalRecords}
          isFetching={isFetching}
          onFirstPagePress={gotoImportListExclusionFirstPage}
          onPreviousPagePress={gotoImportListExclusionPreviousPage}
          onNextPagePress={gotoImportListExclusionNextPage}
          onLastPagePress={gotoImportListExclusionLastPage}
          onPageSelect={gotoImportListExclusionPage}
          {...otherProps}
        />

        <EditImportListExclusionModalConnector
          isOpen={isAddImportListExclusionModalOpen}
          onModalClose={onModalClose}
        />
      </PageSectionContent>
    </FieldSet>
  );
}

export default ImportListExclusions;
