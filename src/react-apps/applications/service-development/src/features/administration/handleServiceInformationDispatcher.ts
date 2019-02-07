import { ActionCreatorsMapObject, bindActionCreators } from 'redux';
import { store } from '../../store';
import * as FetchServiceInformationActions from './handleServiceInformationActions';

/**
 * Define a interface describing the the different Actions available
 * for fetching service.
 */
export interface IHandleServiceInformationDispatchers extends ActionCreatorsMapObject {
  fetchService: (url: string) => FetchServiceInformationActions.IFetchServiceAction;
  fetchServiceFulfilled: (result: any) => FetchServiceInformationActions.IFetchServiceFulfilled;
  fetchServiceRejected: (result: Error) => FetchServiceInformationActions.IFetchServiceRejected;
  fetchServiceName: (url: string) => FetchServiceInformationActions.IFetchServiceNameAction;
  fetchServiceNameFulfilled: (serviceName: string) => FetchServiceInformationActions.IFetchServiceNameFulfilled;
  fetchServiceNameRejected: (error: Error) => FetchServiceInformationActions.IFetchServiceNameRejected;
  saveServiceName: (url: string, newService: string) => FetchServiceInformationActions.ISaveServiceNameAction;
  saveServiceNameFulfilled: (newServiceName: string) => FetchServiceInformationActions.ISaveServiceNameFulfilled;
  saveServiceNameRejected: (error: Error) => FetchServiceInformationActions.ISaveServiceNameRejected;
  fetchInitialCommit: (url: string) => FetchServiceInformationActions.IFetchInitialCommitAction;
  fetchInitialCommitFulfilled: (result: any) => FetchServiceInformationActions.IFetchInitialCommitFulfilled;
  fetchInitialCommitRejected: (error: Error) => FetchServiceInformationActions.IFetchInitialCommitRejected;
}

/**
 * Define mapping between action and Action dispatcher method
 */

const actions: IHandleServiceInformationDispatchers = {
  fetchService: FetchServiceInformationActions.fetchServiceAction,
  fetchServiceFulfilled: FetchServiceInformationActions.fetchServiceFulfilledAction,
  fetchServiceRejected: FetchServiceInformationActions.fetchServiceRejectedAction,
  fetchServiceName: FetchServiceInformationActions.fetchServiceNameAction,
  fetchServiceNameFulfilled: FetchServiceInformationActions.fetchServiceNameFulfilledAction,
  fetchServiceNameRejected: FetchServiceInformationActions.fetchServiceNameRejectedAction,
  saveServiceName: FetchServiceInformationActions.saveServiceNameAction,
  saveServiceNameFulfilled: FetchServiceInformationActions.saveServiceNameFulfilledAction,
  saveServiceNameRejected: FetchServiceInformationActions.saveServiceNameRejectedAction,
  fetchInitialCommit: FetchServiceInformationActions.fetchInitialCommitAction,
  fetchInitialCommitFulfilled: FetchServiceInformationActions.fetchInitialCommitFulfilledAction,
  fetchInitialCommitRejected: FetchServiceInformationActions.fetchInitialCommitRejectedAction,
};

/**
 * Bind action creators to redux store
 */
const HandleServiceInformationActionDispatchers: IHandleServiceInformationDispatchers = bindActionCreators<
  any,
  IHandleServiceInformationDispatchers
>(actions, store.dispatch);

/**
 * Export the dispatcher to be used from REACT components
 */
export default HandleServiceInformationActionDispatchers;
