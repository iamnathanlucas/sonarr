import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import { fetchRootFolders } from 'Store/Actions/rootFolderActions';
import EditSeriesModal from './EditSeriesModal';

const mapDispatchToProps = {
  clearPendingChanges,
  fetchRootFolders
};

class EditSeriesModalConnector extends Component {

  //
  // Lifecycle
  componentDidMount() {
    this.props.fetchRootFolders();
  }

  //
  // Listeners

  onModalClose = () => {
    this.props.clearPendingChanges({ section: 'series' });
    this.props.onModalClose();
  };

  //
  // Render

  render() {
    return (
      <EditSeriesModal
        {...this.props}
        onModalClose={this.onModalClose}
      />
    );
  }
}

EditSeriesModalConnector.propTypes = {
  ...EditSeriesModal.propTypes,
  onModalClose: PropTypes.func.isRequired,
  clearPendingChanges: PropTypes.func.isRequired,
  fetchRootFolders: PropTypes.func.isRequired
};

export default connect(undefined, mapDispatchToProps)(EditSeriesModalConnector);
